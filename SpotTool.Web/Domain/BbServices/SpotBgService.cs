using System.Collections.Concurrent;
using System.Threading.Channels;
using Marten;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Domain.BgServices;

public class SpotBgService : BackgroundService
{
    private readonly Channel<Guid> _wakeupChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly SpotChannelManager _spotChannelManager;
    public SpotBgService(IServiceProvider serviceProvider, Channel<Guid> wakeupChannel, SpotChannelManager spotChannelManager)
    {
        _serviceProvider = serviceProvider;
        _wakeupChannel = wakeupChannel;
        _spotChannelManager = spotChannelManager;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("START");

            var nextSpotDeadline = await GetNextSpotDeadlineAsync(stoppingToken);
            TimeSpan delay = nextSpotDeadline.HasValue ? nextSpotDeadline.Value - DateTimeOffset.UtcNow : TimeSpan.FromMilliseconds(-1);

            if(delay < TimeSpan.Zero && nextSpotDeadline is not null)
                delay = TimeSpan.Zero;

            Console.WriteLine($"Now: {DateTimeOffset.UtcNow}, SpotDL: {nextSpotDeadline}, Next call for: {delay}");

            // 2. Czekaj na nadejście budzika LUB na pojawienie się nowego spota w kanale
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task timerTask = Task.Delay(delay, cts.Token);
            var channelTask = _wakeupChannel.Reader.WaitToReadAsync(cts.Token).AsTask();

            Task completedTask = await Task.WhenAny(timerTask, channelTask);
            cts.Cancel(); // Anuluj drugie zadanie, które jeszcze nie skończyło

            if (completedTask == timerTask && nextSpotDeadline is not null)
            {
                // BUDZIK ZADZWONIŁ! Jakiś spot właśnie się skończył.
                var closedSpots = await UpdateManySpotsStatusAsync(stoppingToken);
                Console.WriteLine($"Zamykam spoty: {string.Join(';', closedSpots)}");
            }
            else
            {
                // Ktoś dodał nowy spot przez API. Pętla `while` się obróci,
                // ponownie sprawdzi bazę i odpowiednio skróci lub wydłuży czas budzika.
                if (_wakeupChannel.Reader.TryRead(out _)) 
                {
                    Console.WriteLine("Wykryto nowy spot, aktualizuję harmonogram.");
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
                m.CurrentStatus == Status.Spot.DuringBidding || 
                m.CurrentStatus == Status.Spot.DuringNegotiation)
            .OrderBy(m=>m.DeadLine)
            .FirstOrDefaultAsync(ct);
        
        return result?.DeadLine;
    }

    private async Task<List<Guid>> UpdateManySpotsStatusAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

        var spotsToUpdate = await _session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.DuringBidding || 
                m.CurrentStatus == Status.Spot.DuringNegotiation &&
                m.DeadLine <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var item in spotsToUpdate)
        {
            Status.Spot spotStatus = Status.Spot.EndedNoOffers;
            var minOrder = await _session.Query<DbModels.Offer>()
                .Where(m=>m.SpotId == item.Id)
                .OrderBy(m=>m.Value)
                .FirstOrDefaultAsync(ct);

            if(minOrder is not null)
                spotStatus = item.TargetedCost >= minOrder.Value ? Status.Spot.EndedCostOK : Status.Spot.EndedCostTooHigh; 
            
            item.CurrentStatus = spotStatus;

            _session.Store(item);

            //Moge stworzyc channel gdzie wysle cala liste!
            await _spotChannelManager.DistributeSpotStatusAsync(item, spotStatus, [], ct);    
        }
        await _session.SaveChangesAsync(ct);

        return [.. spotsToUpdate.Select(m=>m.Id)];
    }
}