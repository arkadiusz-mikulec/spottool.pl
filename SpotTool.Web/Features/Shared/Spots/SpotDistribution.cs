using FastExpressionCompiler.ImTools;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Users;
using static SpotTool.Web.Features.Shared.Channels.SpotChannelManager;


namespace SpotTool.Web.Features.Shared.Spots;

public class SpotDistribution(SpotChannelManager _spotChannelManager, IServiceProvider _serviceProvider)
{
    private readonly string _serviceName = "SpotDistribution";
    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    public Task SendSpotCreatedToAuthorizedUsers(SpotModel spotModel)
    {
        string methodeName = "SendSpotCreatedToAuthorizedUsers";
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
            Console.WriteLine($"[EXCEPTION] in {_serviceName}->{methodeName}. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public void SendSpotCreatedToUser(SpotModel spotModel, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? [], _serviceProvider);
        if(sendSignal)
        {
            var spotUiRespond = new SpotUIRespond(spotModel, userSubscription.UserId, userRole);
            var signal = new SpotSignal(SignalType.SpotCreate, spotUiRespond, null, null);

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
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

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? [], _serviceProvider);
        if(sendSignal)
        {
            var spotUiRespond = new SpotUIRespond(spotModel, userSubscription.UserId, userRole);
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

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? [], _serviceProvider);
        if(sendSignal)
        {
            //Nie musze budowac calego modelu zeby przeslac tylko deadlina update!
            var spotUiDeadLineUpdateRespond = new SpotDeadlineUpdateRespond(spotModel.Spot.Id, spotModel.Spot.DeadLine);
            var signal = new SpotSignal(SignalType.SpotDeadLineUpdate, null, null, null, spotUiDeadLineUpdateRespond);

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }
    
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
        
        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? [], _serviceProvider);
        if(sendSignal)
        {
            var signal = new SpotSignal(
                SignalType.SpotStatusUpdate, 
                new SpotUIRespond(spotModel, userSubscription.UserId, userRole), 
                null,
                null, 
                null
            );

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }

    public Task SendNegotiationEndsToIssuerAndBidder(SpotModel spotModel, Guid[] usersId)
    {
        try
        {
            foreach (var userId in usersId)
            {
                var userData = _spotChannelManager.GetUserSubscriptionData(userId);
                SendSpotStatusToUser(spotModel, userData);
            }    
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Exception]. SpotDistributor.SendNegotiationEndsToIssuerAndBidder. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    public Task SendOfferCreatedToAuthorizedUsers(SpotModel spotModel, DbModels.Offer? offer)
    {
        string methodName = "SendOfferCreatedToAuthorizedUsers";
        try
        {
            var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
            foreach (var user in activeUsers)
                SendOfferCreatedToUser(spotModel, offer, user);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {_serviceName}->{methodName}. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public void SendOfferCreatedToUser(SpotModel spotModel, DbModels.Offer? offer, UserSubscription? userSubscription)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole) = SendTheSignal(spotModel, userSubscription, userSubscription.CarrierGroups ?? [], _serviceProvider);
        if(sendSignal)
        {
            //var spotStatusUi = new SpotStatusUIRespond(spotModel.Spot.Id, userSubscription.UserId, spotModel.Spot.CurrentStatus, userRole, spotModel.Offers);
            var spotUi = new SpotUIRespond(spotModel, userSubscription.UserId, userRole);
            var signal = new SpotSignal(SignalType.OfferCreate, spotUi, null, offer, null); 

            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }
    }

    public void SendOfferRejectedToIssuerAndBidder(SpotModel spotModel, DbModels.Offer offer)
    {
        Guid spotUserId = spotModel.Spot.UserId;
        //bool isNormalOffer = offer.CreatedByUser.Id.Equals(offer.CreatedForUser.Id);

        var issuer = _spotChannelManager.GetUserSubscriptionData(spotUserId);
        var bidder = _spotChannelManager.GetUserSubscriptionData(offer.CreatedForUser.Id); 

        SetSpotUIRespondAndSendSignal(spotModel, issuer, Role.User.Issuer, SignalType.OfferReject, _spotChannelManager);
        SetSpotUIRespondAndSendSignal(spotModel, bidder, Role.User.Bidder, SignalType.OfferReject, _spotChannelManager);

        // if(isNormalOffer)
        // {
        //     issuer = _spotChannelManager.GetUserSubscriptionData(spotUserId);
        //     bidder = _spotChannelManager.GetUserSubscriptionData(offer.CreatedForUser.Id);

        //     SetSpotUIRespondAndSendSignal(spotModel, issuer, Role.User.Issuer, SignalType.OfferReject, _spotChannelManager);
        //     SetSpotUIRespondAndSendSignal(spotModel, bidder, Role.User.Bidder, SignalType.OfferReject, _spotChannelManager);
        // }
        // else
        // {
        //     issuer = _spotChannelManager.GetUserSubscriptionData(spotUserId);
        //     bidder = _spotChannelManager.GetUserSubscriptionData(offer.CreatedForUser.Id);

        //     SetSpotUIRespondAndSendSignal(spotModel, issuer, Role.User.Issuer, SignalType.OfferReject, _spotChannelManager);
        //     SetSpotUIRespondAndSendSignal(spotModel, bidder, Role.User.Bidder, SignalType.OfferReject, _spotChannelManager);
        // }
        
    }
    private static void SetSpotUIRespondAndSendSignal(SpotModel spotModel, UserSubscription? userSubscription, Role.User userRole, SignalType signalType, SpotChannelManager spotChannelManager)
    {
        if(userSubscription is null)
            return;

        var spotUi = new SpotUIRespond(spotModel, userSubscription.UserId, userRole);
        var signal = new SpotSignal(signalType, spotUi, null, null, null); 

        if(!userSubscription.Writer.TryWrite(signal))
            spotChannelManager.Unsubscribe(userSubscription.UserId);
    }

    private static (bool sendSignal, Role.User userRole) SendTheSignal(SpotModel spotModel, UserSubscription userSubscription, List<Guid> carrierGroup,
        IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        bool sendSignal = false;
        Role.User roleForThisSpot = Role.User.Bidder;
        
        if(userService.IsSpotCreator(userSubscription.UserId, spotModel.Spot.UserId))
        {
            sendSignal = true;
            roleForThisSpot = Role.User.Issuer;
        }
        else
        {
            if(userService.CarrierHasAccess(userSubscription.UserId, spotModel.GroupsAssignedToSpot ?? [], carrierGroup, spotModel.UsersExcludedFromSpot ?? []))
                sendSignal = true;
        }
        return new (sendSignal, roleForThisSpot);
    }

    // private static bool IsSpotCreator(Guid channelUserId, Guid spotUserId)
    // {
    //     return channelUserId == spotUserId;
    // }
    // private static bool CarrierHasAccess(Guid userId, List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups, List<Guid> usersExcluded)
    // {
    //     bool isGlobal = groupsAssignedToSpot.Count == 0; 
    //     return isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any() || !usersExcluded.Contains(userId);    
    // }
    // Definicje struktur danych wewnątrz klasy managementu dla lepszej enkapsulacji
    //public record SpotInitModel(Guid connectedUserId, )
 
}