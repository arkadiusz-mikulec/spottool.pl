using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Contracts.UI.Offer;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using static SpotTool.Web.Features.Shared.Channels.SpotChannelManager;


namespace SpotTool.Web.Features.Shared.Spots;

public class SpotDistribution(SpotChannelManager _spotChannelManager, IServiceProvider _serviceProvider)
{

    // Wspólna, prywatna metoda dystrybuująca sygnały do właściwych kanałów personalnych
    public Task SendSpotDataToAuthorizedUsersAsync(DbModels.Spot spot)
    {
        try
        {
            if(spot.CurrentStatus == Status.Spot.NotConfirmed)
            {
                var userData = _spotChannelManager.GetUserSubscriptionData(spot.UserId);
                _ = SendSpotDataToUserAsync(spot, userData, []);
            }
            else
            {
                var groupsAssignedToSpot = new List<Guid>();
                var activeUsers = _spotChannelManager.GetAllUserSubscriptionData();
                foreach (var user in activeUsers)
                {
                    _ = SendSpotDataToUserAsync(spot, user, groupsAssignedToSpot);
                }    
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[EXCEPTION]: {ex.Message} in SpotDistribution.SendSpotDatatoAuthorize");
        }
        return Task.CompletedTask;
    }

    public async Task SendSpotDataToUserAsync(DbModels.Spot spot, UserSubscription? userSubscription, List<Guid> groupsAssignedToSpot)
    {
        if(userSubscription is null)
            return;

        var (sendSignal, userRole, lastBidderOfferRespond) = await SendTheSignal(spot, _serviceProvider, userSubscription, groupsAssignedToSpot);
        if(sendSignal)
        {
            var spotUiRespond = new SpotUIRespond(spot, userRole, lastBidderOfferRespond);
            var signal = new SpotSignal(SignalType.SpotAdded, spotUiRespond, null);

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
                _ = SendSpotStatusToUserAsync(spot, userId, groupsAssignedToSpot);
            }    
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Exception]. SpotDistributor.SendSpotStatusToAuthorizedUsersAsync. Msg: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private async Task SendSpotStatusToUserAsync(DbModels.Spot spot, UserSubscription userSubscription, List<Guid> groupsAssignedToSpot)
    {
        if(userSubscription is null)
            return;
        
        var (sendSignal, userRole, lastBidderOfferRespond) = await SendTheSignal(spot, _serviceProvider, userSubscription, groupsAssignedToSpot);
        if(sendSignal)
        {
            var signal = new SpotSignal(SignalType.SpotStatusUpdate, null, new SpotStatusUIRespond(spot.Id, spot.CurrentStatus, userRole, lastBidderOfferRespond));        
            if(!userSubscription.Writer.TryWrite(signal))
                _spotChannelManager.Unsubscribe(userSubscription.UserId);
        }

    }

    private static async Task<(bool sendSignal, Role.User userRole, LastBidderOfferRespond? lastBidderOfferRespond)>SendTheSignal(DbModels.Spot spot, IServiceProvider serviceProvider, UserSubscription userSubscription, 
        List<Guid> groupsAssignedToSpot)
    {
        bool sendSignal = false;
        Role.User roleForThisSpot = Role.User.Bidder;
        LastBidderOfferRespond? lastBidderOfferRespond = null;

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
            {
                sendSignal = true;
                if(spot.BiddersId is not null && spot.BiddersId.Contains(userSubscription.UserId))
                {
                    using var scope = serviceProvider.CreateScope();
                    IDocumentSession session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

                    var lastBidderOffer = await session.Query<DbModels.Offer>()
                        .Where(m => m.SpotId == spot.Id && m.UserId == userSubscription.UserId)
                        .OrderBy(m => m.Value) // Zamień na Twoje pole sortujące (np. Id lub Date)
                        .FirstOrDefaultAsync();
                    
                    if(lastBidderOffer is not null)
                        lastBidderOfferRespond = new LastBidderOfferRespond(lastBidderOffer!.Value, lastBidderOffer!.ValueStatus);
                }
            }
        }
        return new (sendSignal, roleForThisSpot, lastBidderOfferRespond);
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