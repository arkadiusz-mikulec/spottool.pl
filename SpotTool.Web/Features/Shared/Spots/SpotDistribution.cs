using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using static SpotTool.Web.Features.Shared.Channels.SpotChannelManager;


namespace SpotTool.Web.Features.Shared.Spots;

public class SpotDistribution(SpotChannelManager _spotChannelManager)
{

    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    public Task SendSpotDataToAuthorizedUsersAsync(DbModels.Spot spot)
    {
        try
        {
            if(spot.CurrentStatus == Status.Spot.NotConfirmed)
            {
                var userData = _spotChannelManager.GetUserSubscriptionData(spot.UserId);
                SendSpotDataToUserAsync(spot, userData, []);
            }
            else
            {
                var groupsAssignedToSpot = new List<Guid>();
                var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
                foreach (var user in activeUsers)
                {
                    SendSpotDataToUserAsync(spot, user, groupsAssignedToSpot);
                }    
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {ex.Message} in SpotDistribution.SendSpotDatatoAuthorize");
        }
        return Task.CompletedTask;
    }

    public void SendSpotDataToUserAsync(DbModels.Spot spot, UserSubscription? userSubscription, List<Guid> groupsAssignedToSpot)
    {
        bool sendSignal = false;
        
        if(userSubscription is null)
            return;

        var spotUiRespond = new SpotUIRespond(spot, userSubscription.UserRole);
        var signal = new SpotSignal(SignalType.SpotAdded,  spotUiRespond, null);

        if(userSubscription.UserRole == Role.User.Carrier)
        {
            List<Guid> carrierGroup = []; //_userService.GetGroups or something or casch
            List<Guid> usersExcluded = []; //_spotService
            if(CarrierHasAccess(userSubscription.UserId, groupsAssignedToSpot, carrierGroup, usersExcluded))
                sendSignal = true;
        }
        else if(IsSpotCreator(userSubscription.UserId, spot.UserId))
            sendSignal = true;

        if(sendSignal)
        {
            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }
    //Zmienic tak jak jest zrobione funkcja SendSpotDataToAll
    public Task SendSpotStatusToAuthorizedUsersAsync(DbModels.Spot spot, CancellationToken ct)
    {
        var groupsAssignedToSpot = new List<Guid>();
        var activeUsersIds = _spotChannelManager.GetAllUserSubscriptionData();
        foreach (var userId in activeUsersIds)
        {
            SendSpotStatusToUserAsync(spot, userId, groupsAssignedToSpot);
        }
        return Task.CompletedTask;
    }

    private void SendSpotStatusToUserAsync(DbModels.Spot spot, UserSubscription userSubscription, List<Guid> groupsAssignedToSpot)
    {
        if(userSubscription is null)
            return;

        var signal = new SpotSignal(SignalType.SpotStatusUpdate, null, new SpotStatusUpdateRequest(spot.Id, spot.CurrentStatus));

        if(userSubscription.UserRole == Role.User.Carrier)
        {
            List<Guid> carrierGroup = []; //_userService.GetGroups or something or casch
            List<Guid> usersExcluded = []; //_spotService
            if(CarrierHasAccess(userSubscription.UserId, groupsAssignedToSpot, carrierGroup, usersExcluded))
            {        
                userSubscription.Writer.TryWrite(signal);
            }
        }

        if(IsSpotCreator(userSubscription.UserId, spot.UserId))
            userSubscription.Writer.TryWrite(signal);

    }

    private static bool IsSpotCreator(Guid channelUserId, Guid spotUserId)
    {
        return channelUserId == spotUserId;
    }
    private static bool CarrierHasAccess(Guid userId, List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups, List<Guid> usersExcluded)
    {
        bool isGlobal = groupsAssignedToSpot.Count == 0; 
        return isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any() || !usersExcluded.Contains(userId);    
    }
    // Definicje struktur danych wewnątrz klasy managementu dla lepszej enkapsulacji
    //public record SpotInitModel(Guid connectedUserId, )
 
}