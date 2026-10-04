using System.Collections.Concurrent;
using System.Threading.Channels;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Domain.BgServices;

public class SpotBgService : BackgroundService
{
    private readonly Channel<Guid> _wakeupChannel;
    private readonly IServiceProvider _serviceProvider;
    public SpotBgService(IServiceProvider serviceProvider, Channel<Guid> wakeupChannel)
    {
        _serviceProvider = serviceProvider;
        _wakeupChannel = wakeupChannel;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("START");

            using var scope = _serviceProvider.CreateAsyncScope();
            var _spotService = scope.ServiceProvider.GetRequiredService<SpotService>();

            if(_spotService is null)
            {
                stoppingToken.ThrowIfCancellationRequested();
                Console.WriteLine("Cancelation token throw");
            }

            var nextSpotDeadline = await _spotService!.GetNextSpotDeadlineAsync(stoppingToken);
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
                var closedSpots = await _spotService.UpdateManySpotsStatusAsync(stoppingToken);
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
}