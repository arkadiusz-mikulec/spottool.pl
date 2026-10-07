using System.Collections.Concurrent;
using System.Threading.Channels;
using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Shared.Channels;

public class SpotChannelManager(IServiceProvider _serviceProvider)
{
    private readonly ConcurrentDictionary<Guid, UserSubscription> _activeConnections = [];

    // Rejestracja użytkownika – zwracamy Reader, z którego strona Blazor będzie czytać w pętli 'await foreach'
    public ChannelReader<SpotSignal> Subscribe(Guid userId, List<Guid> carrierGroups)
    {
        var userChannel = Channel.CreateUnbounded<SpotSignal>(new UnboundedChannelOptions
        {
            SingleReader = true // Tylko ta jedna konkretna strona Blazor czyta z tego kanału
        });

        _activeConnections[userId] = new UserSubscription(userId, carrierGroups, userChannel.Writer);
        
        return userChannel.Reader;
    }


    // Wyrejestrowanie użytkownika – bezpiecznie zamykamy jego personalny kanał
    public void Unsubscribe(Guid userId)
    {
        if (_activeConnections.TryRemove(userId, out var sub))
        {
            sub.Writer.Complete(); // Informujemy pętlę na stronie Blazor, że to koniec transmisji
            Console.WriteLine($"Unsubscribe: Connection user ID: {userId}.");
        }
    }
    
    
    // Metoda wywoływana przez Twój BackgroundService, gdy pojawi się nowy spot
    public async Task DistributeSpotAsync(DbModels.Spot spot, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var _spotService = scope.ServiceProvider.GetRequiredService<SpotService>();

        List<Guid> groupsAssignedToSpot = await _spotService.GetSpotGroups(spot.Id, ct);

        var signal = new SpotSignal(SignalType.SpotAdded, spot, null);

        if(spot.CurrentStatus == Status.Spot.NotConfirmed)
            await SendToSpecificUserAsync(spot.UserId, groupsAssignedToSpot, signal, ct);
        else
            await SendToAuthorizedUsersAsync(spot.UserId, groupsAssignedToSpot, signal, ct);
    }

    // Metoda wywoływana przez Twój BackgroundService, gdy zmieni się status spota
    public async Task DistributeSpotStatusAsync(DbModels.Spot spot, Status.Spot status, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var _spotService = scope.ServiceProvider.GetRequiredService<SpotService>();

        List<Guid> groupsAssignedToSpot = await _spotService.GetSpotGroups(spot.Id, ct);


        var updateRequest = new SpotStatusUpdateRequest(spot.Id, status);
        var signal = new SpotSignal(SignalType.SpotStatusUpdate, null, updateRequest);
        
        await SendToAuthorizedUsersAsync(spot.UserId, groupsAssignedToSpot, signal, ct);
    }

    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    private async Task SendToAuthorizedUsersAsync(Guid userId, List<Guid> groupsAssignedToSpot, SpotSignal signal, CancellationToken ct)
    {
        var tasks = _activeConnections.Values.Select(async sub =>
        {
            if(IsSpotCreator(sub.UserId, userId) || CarrierHasAccess(groupsAssignedToSpot, sub.CarrierGroupIds))
            {
                // Zapisujemy bezpośrednio do PRYWATNEGO kanału tego konkretnego użytkownika
                await sub.Writer.WriteAsync(signal, ct);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task SendToSpecificUserAsync(Guid userId, List<Guid> groupsAssignedToSpot, SpotSignal signal, CancellationToken ct)
    {
        var channelUser = _activeConnections.Values.FirstOrDefault(sub => sub.UserId == userId); 

        if(channelUser is null)
            return;
        
        if(IsSpotCreator(channelUser.UserId, userId) || CarrierHasAccess(groupsAssignedToSpot, channelUser.CarrierGroupIds))
            await channelUser.Writer.WriteAsync(signal, ct);
    }

    private static bool IsSpotCreator(Guid channelUserId, Guid spotUserId)
    {
        return channelUserId == spotUserId;
    }
    private static bool CarrierHasAccess(List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups)
    {
        bool isGlobal = groupsAssignedToSpot.Count == 0; 
        return isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any();    
    }
    // Definicje struktur danych wewnątrz klasy managementu dla lepszej enkapsulacji
    //public record SpotInitModel(Guid connectedUserId, )
    
    private record UserSubscription(Guid UserId, List<Guid> CarrierGroupIds, ChannelWriter<SpotSignal> Writer);
    
}