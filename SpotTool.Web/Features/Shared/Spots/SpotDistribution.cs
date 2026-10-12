using Marten;
using Microsoft.AspNetCore.Identity;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Contracts.UI.Offer;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using static SpotTool.Web.Features.Shared.Channels.SpotChannelManager;


namespace SpotTool.Web.Features.Shared.Spots;

public class SpotDistribution(SpotChannelManager _spotChannelManager)
{
    private readonly string _serviceName = "SpotDistribution";
    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    public Task SendSpotCreatedToAuthorizedUsers(SpotModel spotModel)
    {
        try
        {
            if(spotModel.Spot.CurrentStatus == Status.Spot.NotConfirmed)
            {
                var userData = _spotChannelManager.GetUserSubscriptionData(spotModel.Spot.UserId);
                SendSpotCreatedToUser(spotModel, userData);
            }
            else
            {
                var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
                foreach (var user in activeUsers)
                {
                    SendSpotCreatedToUser(spotModel, user);
                }    
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {ex.Message} in SpotDistribution.SendSpotDatatoAuthorize");
        }
        return Task.CompletedTask;
    }

    public void SendSpotCreatedToUser(SpotModel spotModel, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? []);
        if(sendSignal)
        {
            var spotUiRespond = new SpotUIRespond(spotModel, userRole);
            var signal = new SpotSignal(SignalType.SpotCreate, spotUiRespond, null, null);

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }

    public Task SendOfferCreatedToAuthorizedUsers(SpotModel spotModel)
    {
        string methodName = "SendOfferCreatedToAuthorizedUsers";
        try
        {
            var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
            foreach (var user in activeUsers)
                SendOfferCreatedToUser(spotModel, user);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {_serviceName}->{methodName}. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public void SendOfferCreatedToUser(SpotModel spotModel, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? []);
        if(sendSignal)
        {
            var finallOffers = spotModel.Offers;
            if(userRole != Role.User.Issuer && userRole != Role.User.Admin)
                finallOffers  = [.. spotModel!.Offers!.Where(m=>m.CreatedForUser.Id == userSubscription.UserId)];

            var spotStatusUi = new SpotStatusUIRespond(spotModel.Spot.Id, spotModel.Spot.CurrentStatus, userRole, finallOffers);
            var signal = new SpotSignal(SignalType.OfferCreate, null, spotStatusUi, null, null); 

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }

    public void SendOfferRejectedToIssuerAndBidder(Guid spotId, Status.Spot spotStatus, Guid issuerId, Guid bidderId, List<DbModels.Offer> offers)
    {
        
        var issuer = _spotChannelManager.GetUserSubscriptionData(issuerId);
        var bidder = _spotChannelManager.GetUserSubscriptionData(bidderId);

        if(issuer is not null)
        {
            var statusUIRespond = new SpotStatusUIRespond(spotId, spotStatus, Role.User.Issuer, offers);        
            var signal = new SpotSignal(SignalType.OfferReject, null, statusUIRespond, null, null); 

            if(!issuer.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(issuer.UserId);
        }
            
        if(bidder is not null)
        {
            var statusUIRespond = new SpotStatusUIRespond(spotId, spotStatus, Role.User.Bidder, offers);        
            var signal = new SpotSignal(SignalType.OfferReject, null, statusUIRespond, null, null); 

            if(!bidder.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(bidder.UserId);
        }
            
    }

    public Task SendSpotApproveToAuthorizedUsers(SpotModel spotModel)
    {
        string methodName = "SendSpotApproveToAuthorizedUsers";
        try
        {
            var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
            foreach (var user in activeUsers)
            {
                SendSpotApproveToUser(spotModel, user);
            }    
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {_serviceName}->{methodName}. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    public void SendSpotApproveToUser(SpotModel spotModel, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? []);
        if(sendSignal)
        {
            var spotUiRespond = new SpotUIRespond(spotModel, userRole);
            var signal = new SpotSignal(SignalType.SpotApprove, spotUiRespond, null, null);

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }
    public Task SendSpotDeadlineUpdateToAuthorizedUsers(SpotModel spotModel)
    {
        string methodName = "SendSpotDeadlineUpdateToAuthorizedUsers";
        try
        {
            var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
            foreach (var user in activeUsers)
            {
                SendSpotDeadlineUpdateToUser(spotModel, user);
            }    
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {_serviceName}->{methodName}. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    public void SendSpotDeadlineUpdateToUser(SpotModel spotModel, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? []);
        if(sendSignal)
        {
            //Nie musze budowac calego modelu zeby przeslac tylko deadlina update!
            var spotUiDeadLineUpdateRespond = new SpotDeadlineUpdateRespond(spotModel.Spot.Id, spotModel.Spot.DeadLine);
            var signal = new SpotSignal(SignalType.SpotDeadLineUpdate, null, null, null, spotUiDeadLineUpdateRespond);

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }
    //Zmienic tak jak jest zrobione funkcja SendSpotDataToAll
    public Task SendSpotStatusToAuthorizedUsers(SpotModel spotModel)
    {
        try
        {
            var activeUsersData = _spotChannelManager.GetAllUserSubscriptionData();
            foreach (var userData in activeUsersData)
            {
                SendSpotStatusToUser(spotModel, userData);
            }    
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Exception]. SpotDistributor.SendSpotStatusToAuthorizedUsersAsync. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private void SendSpotStatusToUser(SpotModel spotModel, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;
        
        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? []);
        if(sendSignal)
        {

            var signal = new SpotSignal(
                SignalType.SpotStatusUpdate, 
                null, 
                new SpotStatusUIRespond(spotModel.Spot.Id, spotModel.Spot.CurrentStatus, userRole, null),
                null, null
            );

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }

    }

    private static (bool sendSignal, Role.User userRole) SendTheSignal(SpotModel spotModel, UserSubscription userSubscription, List<Guid> carrierGroup)
    {
        bool sendSignal = false;
        Role.User roleForThisSpot = Role.User.Bidder;
        
        if(IsSpotCreator(userSubscription.UserId, spotModel.Spot.UserId))
        {
            sendSignal = true;
            roleForThisSpot = Role.User.Issuer;
        }
        else
        {
            if(CarrierHasAccess(userSubscription.UserId, spotModel.GroupsAssignedToSpot ?? [], carrierGroup, spotModel.UsersExcludedFromSpot ?? []))
                sendSignal = true;
        }
        return new (sendSignal, roleForThisSpot);
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