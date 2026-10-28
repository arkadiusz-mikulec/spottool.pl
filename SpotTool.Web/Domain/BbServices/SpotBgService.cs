using System.Collections.Concurrent;
using System.Threading.Channels;
using Marten;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Offers;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Domain.BgServices;

public class SpotBgService : BackgroundService
{
    private readonly Channel<Guid> _wakeupChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly SpotDistribution _spotDistribution;
    private readonly IDateTimeProvider _datetimeProvoder;
    public SpotBgService(IServiceProvider serviceProvider, Channel<Guid> wakeupChannel, IDateTimeProvider datetimeProvoder, SpotDistribution spotDistribution)
    {
        _serviceProvider = serviceProvider;
        _wakeupChannel = wakeupChannel;
        _datetimeProvoder = datetimeProvoder;
        _spotDistribution = spotDistribution;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("START");

            var nextSpotDeadline = await GetNextSpotDeadlineAsync(stoppingToken);
            var nextNegotiationData = await GetNextNegotiationDeadlineAsync(stoppingToken);
            var currentTime = _datetimeProvoder.Now();
            bool isSpotToClose = true;

            TimeSpan delay = TimeSpan.FromMilliseconds(-1);
            if(nextSpotDeadline.HasValue && nextNegotiationData.HasValue)
            {
                delay = nextSpotDeadline.Value > nextNegotiationData.Value.NextTick ? 
                    nextNegotiationData.Value.NextTick - currentTime :
                    nextSpotDeadline.Value - currentTime;
                
                isSpotToClose = nextSpotDeadline.Value < nextNegotiationData.Value.NextTick;
            }
            else if(nextSpotDeadline.HasValue && !nextNegotiationData.HasValue)
                delay =  nextSpotDeadline.Value - currentTime;
            else if(!nextSpotDeadline.HasValue && nextNegotiationData.HasValue)
            {
                isSpotToClose = false;
                delay =  nextNegotiationData.Value.NextTick - currentTime;
            }

            if(delay < TimeSpan.Zero && (nextSpotDeadline.HasValue || nextNegotiationData.HasValue))
                delay = TimeSpan.Zero;

            Console.WriteLine($"Now: {currentTime}, SpotDlHasValue: {nextSpotDeadline.HasValue}, NegDeadLine: {nextNegotiationData.HasValue}, Next call for: {delay}");

            // 2. Czekaj na nadejście budzika LUB na pojawienie się nowego spota w kanale
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task timerTask = Task.Delay(delay, cts.Token);
            var channelTask = _wakeupChannel.Reader.WaitToReadAsync(cts.Token).AsTask();

            Task completedTask = await Task.WhenAny([timerTask, channelTask]);    //WhenAny(timerTask, channelTask);
            cts.Cancel(); // Anuluj drugie zadanie, które jeszcze nie skończyło

            if (completedTask == timerTask)
            {
                // BUDZIK ZADZWONIŁ! Jakiś spot właśnie się skończył.
                if(isSpotToClose)
                {
                    var closedSpots = await UpdateManySpotsStatusAsync(stoppingToken);
                    Console.WriteLine($"Zamykam spoty: {string.Join(';', closedSpots)}");    
                }
                else
                {
                    var (IsError, ErrorMsg, SpotId) = await UpdateSpotStatusAfterNegotiationsAsync(nextNegotiationData!.Value.OfferId, stoppingToken);
                    if(!IsError)
                        Console.WriteLine($"Zamykam spota z negocjacji: {SpotId}");
                    else
                        Console.WriteLine($"Error podczas zamykam spota z negocjacji!. Msg: {ErrorMsg}");
                }   
                
            }
            else
            {
                // Ktoś dodał nowy spot przez API. Pętla `while` się obróci,
                // ponownie sprawdzi bazę i odpowiednio skróci lub wydłuży czas budzika.
                if (_wakeupChannel.Reader.TryRead(out var spotId)) 
                {
                    Console.WriteLine($"Dodano lub zaktualizowano spotId: {spotId}. Aktualizuję harmonogram.");
                }
            }
        }
    }

    private async Task<DateTimeOffset?> GetNextSpotDeadlineAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();


        var result = await _session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.Bidding)
            .OrderBy(m=>m.DeadLine)
            .FirstOrDefaultAsync(ct);

        return result?.DeadLine;
    }

    private async Task<(Guid OfferId, DateTimeOffset NextTick)?> GetNextNegotiationDeadlineAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var _session = scope.ServiceProvider.GetRequiredService<IQuerySession>();

        var negotiationSpotId = await _session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Negotiation).Select(m=>m.Id).ToListAsync(ct);

        if(negotiationSpotId is null || negotiationSpotId.Count == 0)
            return null;
        
        var nextDedLine = await _session.Query<DbModels.Offer>()
            .Where(m=>m.Status == Status.Offer.ToNegotiation && negotiationSpotId.Contains(m.SpotId))
            .OrderBy(m=>m.ValidTill)
            .FirstOrDefaultAsync(ct);

        if(nextDedLine is null)
            return null;
        return (nextDedLine.Id, nextDedLine.ValidTill);
    }

    private async Task<IReadOnlyList<Guid>> UpdateManySpotsStatusAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var spotService = scope.ServiceProvider.GetRequiredService<SpotService>();
        var offerService = scope.ServiceProvider.GetRequiredService<OfferService>();
        var currentTime = _datetimeProvoder.Now();

        var spots = await session.Query<DbModels.Spot>()
            .Where(m=>(m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.Bidding) &&
                m.DeadLine <= currentTime)
            .ToListAsync(ct);
    
        foreach (var spot in spots)
        {
            Status.Spot spotStatus = Status.Spot.EndedNoOffer;

            if(spot.BiddersId is not null && spot.BiddersId.Count > 0)
                spotStatus = Status.Spot.Ended; //item.TargetedCost >= offers[0].Value ? Status.Spot.EndedCostOK : Status.Spot.EndedCostTooHigh; 
            
            spot.CurrentStatus = spotStatus;

            session.Store(spot);
            //Sprawdzic czy to musi byc tak zapisywane ale raczej tak
            await session.SaveChangesAsync(ct);
                
            var spotModel = new SpotModel(
                spot, 
                await offerService.GetLastOffersForSpotAsync(spot, ct), 
                await spotService.GetSpotGroups(spot.Id, ct),
                await spotService.GetUsersExcluded(spot.Id, ct)
            );
            _ = _spotDistribution.SendSpotStatusToAuthorizedUsers(spotModel);
        }       
        return [.. spots.Select(m=>m.Id)];
    }

    //Funkcja aktualizuje status ofertu której czas upłynął i jeżeli nie ma juz wiecej ofert negocjacyjnych zamyka spota 
    private async Task<(bool IsError, string? ErrorMsg, Guid? SpotId)> UpdateSpotStatusAfterNegotiationsAsync(Guid offerId, CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var spotService = scope.ServiceProvider.GetRequiredService<SpotService>();
        var offerService = scope.ServiceProvider.GetRequiredService<OfferService>();
        var currentTime = _datetimeProvoder.Now();
        
        DbModels.Offer? offer = await session.LoadAsync<DbModels.Offer>(offerId, ct);

        if(offer is null)
            return (true, "Offer not found", null);

        offer.Status = Status.Offer.Rejected;
        session.Store(offer);
        await session.SaveChangesAsync(ct);

        var otherDuringNegotiation = await session.Query<DbModels.Offer>()
            .AnyAsync(m=>m.SpotId == offer.SpotId && m.Status == Status.Offer.ToNegotiation, ct);
        
        if(!otherDuringNegotiation)
        {
            var spot = await session.LoadAsync<DbModels.Spot>(offer.SpotId, ct);
            if(spot is null)
                return (true, "Spot is null", null);

            Status.Spot spotStatus = Status.Spot.EndedNoOffer;

            if(spot.BiddersId is not null && spot.BiddersId.Count > 0)
                spotStatus = Status.Spot.Ended; //item.TargetedCost >= offers[0].Value ? Status.Spot.EndedCostOK : Status.Spot.EndedCostTooHigh; 
            
            spot.CurrentStatus = spotStatus;
            session.Store(spot);

            await session.SaveChangesAsync(ct);
                
            var spotModel = new SpotModel(
                spot, 
                await offerService.GetLastOffersForSpotAsync(spot, ct), 
                await spotService.GetSpotGroups(spot.Id, ct),
                await spotService.GetUsersExcluded(spot.Id, ct)
            );
            _ = _spotDistribution.SendNegotiationEndsToIssuerAndBidder(spotModel, [spot.UserId, offer.CreatedForUser.Id]);

            return (false, null, spot.Id);
        }
        
        return (false, null, null);
    }
}