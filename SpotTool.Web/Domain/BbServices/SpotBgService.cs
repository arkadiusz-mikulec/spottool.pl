using System.Collections.Concurrent;
using System.Threading.Channels;
using Marten;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
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
            var nextNegotiationDeadline = await GetNextNegotiationDeadlineAsync(stoppingToken);
            var currentTime = _datetimeProvoder.Now();
            bool isSpotDeadline = true;

            TimeSpan delay = TimeSpan.FromMilliseconds(-1);
            if(nextSpotDeadline.HasValue && nextNegotiationDeadline.HasValue)
            {
                delay =  nextSpotDeadline.Value > nextNegotiationDeadline.Value ? 
                    nextNegotiationDeadline.Value - currentTime :
                    nextSpotDeadline.Value - currentTime;
                
                isSpotDeadline = nextSpotDeadline.Value < nextNegotiationDeadline.Value;
            }
            else if(nextSpotDeadline.HasValue && !nextNegotiationDeadline.HasValue)
                delay =  nextSpotDeadline.Value - currentTime;
            else if(!nextSpotDeadline.HasValue && nextNegotiationDeadline.HasValue)
            {
                isSpotDeadline = false;
                delay =  nextNegotiationDeadline.Value - currentTime;
            }

            if(delay < TimeSpan.Zero && (nextSpotDeadline.HasValue || nextNegotiationDeadline.HasValue))
                delay = TimeSpan.Zero;

            Console.WriteLine($"Now: {currentTime}, SpotDLHasValue: {nextSpotDeadline.HasValue}, NeGdeadLine: {nextNegotiationDeadline.HasValue}, Next call for: {delay}");

            // 2. Czekaj na nadejście budzika LUB na pojawienie się nowego spota w kanale
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task timerTask = Task.Delay(delay, cts.Token);
            var channelTask = _wakeupChannel.Reader.WaitToReadAsync(cts.Token).AsTask();

            Task completedTask = await Task.WhenAny([timerTask, channelTask]);    //WhenAny(timerTask, channelTask);
            cts.Cancel(); // Anuluj drugie zadanie, które jeszcze nie skończyło

            if (completedTask == timerTask)
            {
                // BUDZIK ZADZWONIŁ! Jakiś spot właśnie się skończył.
                var closedSpots = await UpdateManySpotsStatusAsync(isSpotDeadline, stoppingToken);
                Console.WriteLine($"Zamykam spoty: {string.Join(';', closedSpots)}");
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
        using var scope = _serviceProvider.CreateAsyncScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();


        var result = await _session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.Bidding)
            .OrderBy(m=>m.DeadLine)
            .FirstOrDefaultAsync(ct);

        return result?.DeadLine;
    }

    private async Task<DateTimeOffset?> GetNextNegotiationDeadlineAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

        var resultNegotiation = await _session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Negotiation)
            .OrderBy(m=>m.NegotiationDeadLine)
            .FirstOrDefaultAsync(ct);

        return resultNegotiation?.NegotiationDeadLine;
    }

    private async Task<List<Guid>> UpdateManySpotsStatusAsync(bool isSpotDeadline, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var spotService = scope.ServiceProvider.GetRequiredService<SpotService>();

        var currentTime = _datetimeProvoder.Now();

        List<DbModels.Spot> spotsToUpdate = [];
        
        if(isSpotDeadline)
        {
            spotsToUpdate = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                    m.CurrentStatus == Status.Spot.Bidding &&
                    m.DeadLine <= currentTime)
                .ToListAsync(ct)
            ];
        }
        else
        {
            spotsToUpdate = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus == Status.Spot.Negotiation &&
                    m.NegotiationDeadLine <= currentTime)
                .ToListAsync(ct)
            ];
        }
        
        foreach (var item in spotsToUpdate)
        {
            Status.Spot spotStatus = Status.Spot.EndedNoOffers;
            
            var offers = await _session.Query<DbModels.Offer>()
                .Where(m=>m.SpotId == item.Id && m.CreatedByUser.Id != item.UserId)
                .OrderBy(m=>m.Value)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct);

            if(offers.Count > 0)
                spotStatus = item.TargetedCost >= offers[0].Value ? Status.Spot.EndedCostOK : Status.Spot.EndedCostTooHigh; 
            
            item.CurrentStatus = spotStatus;

            _session.Store(item);

            //Moge stworzyc channel gdzie wysle cala liste!
            var spotModel = new SpotModel(
                item, 
                offers, 
                await spotService.GetSpotGroups(item.Id, ct),
                await spotService.GetUsersExcluded(item.Id, ct)
            );
            _ = _spotDistribution.SendSpotStatusToAuthorizedUsers(spotModel);
        }
        await _session.SaveChangesAsync(ct);

        return [.. spotsToUpdate.Select(m=>m.Id)];
    }
}