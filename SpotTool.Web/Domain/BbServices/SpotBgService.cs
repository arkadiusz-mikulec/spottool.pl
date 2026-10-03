using System.Threading.Channels;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Domain.BgServices;

public class SpotBgService : BackgroundService
{
    private readonly Channel<DbModels.Spot> _spotChannel;
    private readonly IServiceProvider _serviceProvider;
    public SpotBgService(IServiceProvider serviceProvider, Channel<DbModels.Spot> spotChannel)
    {
        _spotChannel = spotChannel;
        _serviceProvider = serviceProvider;
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

            var spotInfo = await _spotService!.GetNextSpotDeadlineAndIdAsync(stoppingToken);
            TimeSpan delay = spotInfo is null ? TimeSpan.FromMilliseconds(-1) : spotInfo.Item1 - DateTimeOffset.UtcNow;

            if(delay < TimeSpan.Zero && spotInfo is not null)
                delay = TimeSpan.Zero;

            Console.WriteLine($"Now: {DateTimeOffset.UtcNow}, SpotDL: {spotInfo?.Item1}, Next call for: {delay}");

            // 2. Czekaj na nadejście budzika LUB na pojawienie się nowego spota w kanale
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task timerTask = Task.Delay(delay, cts.Token);
            var channelTask = _spotChannel.Reader.WaitToReadAsync(cts.Token).AsTask();

            Task completedTask = await Task.WhenAny(timerTask, channelTask);
            cts.Cancel(); // Anuluj drugie zadanie, które jeszcze nie skończyło

            if (completedTask == timerTask && spotInfo is not null)
            {
                // BUDZIK ZADZWONIŁ! Jakiś spot właśnie się skończył.
                await _spotService.UpdateSpotStatusAsync(spotInfo.Item2, stoppingToken);
                Console.WriteLine($"Zamykam spotID {spotInfo.Item2}");
            }
            else
            {
                // Ktoś dodał nowy spot przez API. Pętla `while` się obróci,
                // ponownie sprawdzi bazę i odpowiednio skróci lub wydłuży czas budzika.
                if (_spotChannel.Reader.TryRead(out _)) 
                {
                    Console.WriteLine("Wykryto nowy spot, aktualizuję harmonogram.");
                }
            }
        }
    }
}