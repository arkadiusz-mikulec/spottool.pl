using System.Collections.Concurrent;
using System.Threading.Channels;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Features.Shared.Channels;

public class SpotChannelManager()
{
    private readonly ConcurrentDictionary<Guid, UserSubscription> _activeConnections = [];

    // Rejestracja użytkownika – zwracamy Reader, z którego strona Blazor będzie czytać w pętli 'await foreach'
    public ChannelReader<SpotSignal> Subscribe(Guid connectionId, Guid userId, List<Guid> carrierGroups)
    {
        var userChannel = Channel.CreateUnbounded<SpotSignal>(new UnboundedChannelOptions
        {
            SingleReader = true // Tylko ta jedna konkretna strona Blazor czyta z tego kanału
        });

        _activeConnections[connectionId] = new UserSubscription(userId, carrierGroups, userChannel.Writer);
        
        return userChannel.Reader;
    }


    // Wyrejestrowanie użytkownika – bezpiecznie zamykamy jego personalny kanał
    public void Unsubscribe(Guid connectionId)
    {
        if (_activeConnections.TryRemove(connectionId, out var sub))
        {
            sub.Writer.Complete(); // Informujemy pętlę na stronie Blazor, że to koniec transmisji
            Console.WriteLine($"Unsubscribe: Connection {connectionId} closed.");
        }
    }

    // Metoda wywoływana przez Twój BackgroundService, gdy pojawi się nowy spot
    public async Task DistributeSpotAsync(DbModels.Spot spot, List<Guid> groupsAssignedToSpot, CancellationToken ct)
    {
        var signal = new SpotSignal(SignalType.SpotAdded, spot, null);
        await SendToAuthorizedUsersAsync(spot.UserId, groupsAssignedToSpot, signal, ct);
    }

    // Metoda wywoływana przez Twój BackgroundService, gdy zmieni się status spota
    public async Task DistributeSpotStatusAsync(DbModels.Spot spot, Status.Spot status, List<Guid> groupsAssignedToSpot, CancellationToken ct)
    {
        var updateRequest = new SpotStatusUpdateRequest(spot.Id, status);
        var signal = new SpotSignal(SignalType.SpotStatusUpdate, null, updateRequest);
        
        await SendToAuthorizedUsersAsync(spot.UserId, groupsAssignedToSpot, signal, ct);
    }

    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    private async Task SendToAuthorizedUsersAsync(Guid spotOwnerId, List<Guid> groupsAssignedToSpot, SpotSignal signal, CancellationToken ct)
    {
        var tasks = _activeConnections.Values.Select(async sub =>
        {
            if (UserHasAccess(sub.UserId, spotOwnerId, groupsAssignedToSpot, sub.CarrierGroupIds))
            {
                // Zapisujemy bezpośrednio do PRYWATNEGO kanału tego konkretnego użytkownika
                await sub.Writer.WriteAsync(signal, ct);
            }
        });

        await Task.WhenAll(tasks);
    }

    private static bool UserHasAccess(Guid userId, Guid spotUserId, List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups)
    {
        bool isOwner = userId == spotUserId;
        bool isGlobal = groupsAssignedToSpot.Count == 0; 
        return isOwner || isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any();
    }
    // Definicje struktur danych wewnątrz klasy managementu dla lepszej enkapsulacji
    public enum SignalType
    {
        SpotAdded,
        SpotStatusUpdate
    }
    // Generyczny kontener przesyłający akcję oraz powiązany z nią obiekt danych
    public record SpotSignal(SignalType Type, DbModels.Spot? Spot, SpotStatusUpdateRequest? StatusUpdate);
    
    private record UserSubscription(Guid UserId, List<Guid> CarrierGroupIds, ChannelWriter<SpotSignal> Writer);
    
}