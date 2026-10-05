using System.Collections.Concurrent;
using System.Threading.Channels;
using Marten;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

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
    public async Task InitSpotsAsync(Guid connectedUserId, Role.User userRole, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        List<DbModels.Spot> spots = [];

        if(userRole == Role.User.Disponent)
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.UserId == connectedUserId &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];
        }
        else if(userRole == Role.User.Admin || userRole == Role.User.Manager)
        {
            //Jakies ograniczenia dla teamu!!!
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];
        }
        else if(userRole == Role.User.Carrier)
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus == Status.Spot.Ok &&
                    m.CurrentStatus != Status.Spot.Deleted &&
                    m.CurrentStatus != Status.Spot.EndedNoOffers &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];

            List<DbModels.Spot> restOfSpots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Ok &&
                    m.CurrentStatus != Status.Spot.Deleted &&
                    m.CurrentStatus != Status.Spot.EndedNoOffers &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];

            foreach (var spot in restOfSpots)
            {
                var offerExist = await _session.Query<DbModels.Offer>()
                    .AnyAsync(m=>m.CreatedForUser.Id == connectedUserId && m.SpotId == spot.Id, ct);

                if(offerExist)
                    spots.Add(spot);    
            }
        }
        
        foreach (var spot in spots)
        {
            var groupsAssignedToSpot = new List<Guid>();
            var signal = new SpotSignal(SignalType.SpotAdded, spot, null);
            await SendToSpecificUserAsync(connectedUserId, groupsAssignedToSpot, signal, ct);
        }
    }
    // Metoda wywoływana przez Twój BackgroundService, gdy pojawi się nowy spot
    public async Task DistributeSpotAsync(DbModels.Spot spot, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

        List<Guid> groupsAssignedToSpot = [];

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
        var _session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

        List<Guid> groupsAssignedToSpot = [];

        var updateRequest = new SpotStatusUpdateRequest(spot.Id, status);
        var signal = new SpotSignal(SignalType.SpotStatusUpdate, null, updateRequest);
        
        await SendToAuthorizedUsersAsync(spot.UserId, groupsAssignedToSpot, signal, ct);
    }

    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    private async Task SendToAuthorizedUsersAsync(Guid spotUserId, List<Guid> groupsAssignedToSpot, SpotSignal signal, CancellationToken ct)
    {
        var tasks = _activeConnections.Values.Select(async sub =>
        {
            if (UserHasAccess(sub.UserId, spotUserId, groupsAssignedToSpot, sub.CarrierGroupIds))
            {
                // Zapisujemy bezpośrednio do PRYWATNEGO kanału tego konkretnego użytkownika
                await sub.Writer.WriteAsync(signal, ct);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task SendToSpecificUserAsync(Guid userId, List<Guid> groupsAssignedToSpot, SpotSignal signal, CancellationToken ct)
    {
        var spotUser = _activeConnections.Values.FirstOrDefault(sub => sub.UserId == userId); 

        if(spotUser is null)
            return;
        
        if(UserHasAccess(spotUser.UserId, userId, groupsAssignedToSpot, spotUser.CarrierGroupIds))
            await spotUser.Writer.WriteAsync(signal, ct);
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
    //public record SpotInitModel(Guid connectedUserId, )
    
    private record UserSubscription(Guid UserId, List<Guid> CarrierGroupIds, ChannelWriter<SpotSignal> Writer);
    
}