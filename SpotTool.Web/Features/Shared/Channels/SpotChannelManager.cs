using System.Collections.Concurrent;
using System.Threading.Channels;
using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Shared.Channels;

public class SpotChannelManager()
{
    private readonly ConcurrentDictionary<Guid, UserSubscription> _activeConnections = [];

    // Rejestracja użytkownika – zwracamy Reader, z którego strona Blazor będzie czytać w pętli 'await foreach'
    public ChannelReader<SpotSignal> Subscribe(Guid userId, Role.User userRole)
    {
        var userChannel = Channel.CreateUnbounded<SpotSignal>(new UnboundedChannelOptions
        {
            SingleReader = true // Tylko ta jedna konkretna strona Blazor czyta z tego kanału
        });

        _activeConnections[userId] = new UserSubscription(userId, userRole, userChannel.Writer);
        
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
    
    public UserSubscription? GetUserSubscriptionData(Guid connectionId)
    {
        return _activeConnections.TryGetValue(connectionId, out var sub) ? sub : null;
    }

    // public List<Guid> GetAllActiveConnectionsId()
    // {
    //     return [.. _activeConnections.Keys];
    // }

    public IEnumerable<UserSubscription> GetAllUserSubscriptionData()
    {
        return _activeConnections.Values;
    }
       
    public record UserSubscription(Guid UserId, Role.User UserRole, ChannelWriter<SpotSignal> Writer);
    
}