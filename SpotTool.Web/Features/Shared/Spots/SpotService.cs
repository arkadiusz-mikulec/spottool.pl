using FastExpressionCompiler.ImTools;
using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;


namespace SpotTool.Web.Features.Shared.Spots;

//docelowo to bedzie in memory casch
public class SpotService(IDocumentSession _session,
    SpotDistribution _spotDistribution,
    SpotChannelManager _spotChannelManager)
{
    // public async Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct)
    // {
    //     //using var session = _store.QuerySession();
    //     var spot = await _session.LoadAsync<DbModels.Spot>(id, ct);

    //     if (spot is null)
    //         return null;
    //     return spot;
    // }

    // public async Task<List<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    // {
    //     //using var session = _store.QuerySession();
    //     return await _session.Query<DbModels.Spot>().ToAsyncEnumerable(ct).ToListAsync(ct);
    // }

    //To powinno byc brane z Casch po zatwierdzeniu spota grupy nie moga ulec zmianie!
    public async Task<List<Guid>> GetSpotGroups(Guid id, CancellationToken ct)
    {
        return [];
    }
    public async Task<List<Guid>> GetUsersExcluded(Guid id, CancellationToken ct)
    {
        return [];
    }
    public async Task<List<SpotUIRespond>> InitSpotsAsync(Guid connectedUserId, List<Role.User> userRoles, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {
        List<DbModels.Spot> spots = [];

        if(userRoles.Contains(Role.User.Admin))
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToListAsync(ct)
            ];
        }
        else if(userRoles.Contains(Role.User.Issuer) && userRoles.Contains(Role.User.Bidder))
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.UserId == connectedUserId &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToListAsync(ct)
            ];
            // all spot from date range with current user offer 
            spots.AddRange([.. await _session.Query<DbModels.Spot>()
                .Where(m=>(m.CurrentStatus == Status.Spot.Ok ||
                    m.CurrentStatus == Status.Spot.Bidding ||
                    m.BiddersId!.Contains(connectedUserId)) &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToListAsync(ct)
                ]
            );
        }
        else if(userRoles.Contains(Role.User.Issuer))
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.UserId == connectedUserId &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToListAsync(ct)
            ];
        }
        else if(userRoles.Contains(Role.User.Bidder))
        {
            // all spot from date range with current user offer 
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>(m.CurrentStatus == Status.Spot.Ok ||
                    m.CurrentStatus == Status.Spot.Bidding ||
                    m.BiddersId!.Contains(connectedUserId)) &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToListAsync(ct)
            ];
        }        
        
        List<SpotUIRespond> spotUIs = [];
        foreach (var spot in spots)
        {
            
            // var groupsAssignedToSpot = await GetSpotGroups(spot.Id, ct);
            // var usersExcluded = await GetUsersExcluded(spot.Id, ct);

            //var userSubscibed = _spotChannelManager.GetUserSubscriptionData(connectedUserId);
            
            List<DbModels.Offer>? offers = [.. await _session.Query<DbModels.Offer>()
                    .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
                    .ToListAsync(ct)
                ];

            Role.User userRole = IsSpotCreator(connectedUserId, spot.UserId) ? Role.User.Issuer : Role.User.Bidder;
            var spotModel = new SpotModel(spot, offers, null, null);
            spotUIs.Add(new SpotUIRespond(spotModel, connectedUserId, userRole));

            //_spotDistribution.SendSpotCreatedToUser(spotModel, userSubscibed);
        }
        return [.. spotUIs.OrderBy(m=>m.SpotModel.Spot.DeadLine)];
    }
    private static bool IsSpotCreator(Guid userId, Guid spotUserId)
    {
        //tutaj bedzie cala logika dotyczaca BU!
        return userId == spotUserId;
    }
    // private static bool CarrierHasAccess(Guid userId, List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups, List<Guid> usersExcluded)
    // {
    //     bool isGlobal = groupsAssignedToSpot.Count == 0; 
    //     return isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any() || !usersExcluded.Contains(userId);    
    // }
}