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
        Role.User roleForThisSpot = Role.User.Bidder;

        if(userSubscription is null)
            return;

        if(IsSpotCreator(userSubscription.UserId, spot.UserId))
        {
            sendSignal = true;
            roleForThisSpot = Role.User.Issuer;
        }
        else
        {
            List<Guid> carrierGroup = []; //_userService.GetGroups or something or casch
            List<Guid> usersExcluded = []; //_spotService
            if(CarrierHasAccess(userSubscription.UserId, groupsAssignedToSpot, carrierGroup, usersExcluded))
                sendSignal = true;
        }

        if(sendSignal)
        {
            var spotUiRespond = new SpotUIRespond(spot, roleForThisSpot);
            var signal = new SpotSignal(SignalType.SpotAdded,  spotUiRespond, null);

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }
    //Zmienic tak jak jest zrobione funkcja SendSpotDataToAll
    public Task SendSpotStatusToAuthorizedUsersAsync(DbModels.Spot spot, CancellationToken ct)
    {
        try
        {
            var groupsAssignedToSpot = new List<Guid>();
            var activeUsersIds = _spotChannelManager.GetAllUserSubscriptionData();
            foreach (var userId in activeUsersIds)
            {
                SendSpotStatusToUserAsync(spot, userId, groupsAssignedToSpot);
            }    
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Exception]. SpotDistributor.SendSpotStatusToAuthorizedUsersAsync. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private void SendSpotStatusToUserAsync(DbModels.Spot spot, UserSubscription userSubscription, List<Guid> groupsAssignedToSpot)
    {
        bool sendSignal = false;
        Role.User roleForThisSpot = Role.User.Bidder;

        if(userSubscription is null)
            return;

        if(IsSpotCreator(userSubscription.UserId, spot.UserId))
        {
            sendSignal = true;
            roleForThisSpot = Role.User.Issuer;
        }
        else
        {
            List<Guid> carrierGroup = []; //_userService.GetGroups or something or casch
            List<Guid> usersExcluded = []; //_spotService
            if(CarrierHasAccess(userSubscription.UserId, groupsAssignedToSpot, carrierGroup, usersExcluded))
                sendSignal = true;
        }
            
        if(sendSignal)
        {
            var signal = new SpotSignal(SignalType.SpotStatusUpdate, null, new SpotStatusUpdateRequest(spot.Id, spot.CurrentStatus));        
            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
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