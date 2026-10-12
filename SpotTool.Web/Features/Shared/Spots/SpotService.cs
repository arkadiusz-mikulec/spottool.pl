using FastExpressionCompiler.ImTools;
using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;


namespace SpotTool.Web.Features.Shared.Spots;

public class SpotService(IDocumentSession _session,
    SpotDistribution _spotDistribution,
    SpotChannelManager _spotChannelManager)
{
    public async Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct)
    {
        //using var session = _store.QuerySession();
        var spot = await _session.LoadAsync<DbModels.Spot>(id, ct);

        if (spot is null)
            return null;
        return spot;
    }

    public async Task<List<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    {
        //using var session = _store.QuerySession();
        return await _session.Query<DbModels.Spot>().ToAsyncEnumerable(ct).ToListAsync(ct);
    }

    //To powinno byc brane z Casch po zatwierdzeniu spota grupy nie moga ulec zmianie!
    public async Task<List<Guid>> GetSpotGroups(Guid id, CancellationToken ct)
    {
        return [];
    }
    public async Task<List<Guid>> GetUsersExcluded(Guid id, CancellationToken ct)
    {
        return [];
    }
    public async Task InitSpotsAsync(Guid connectedUserId, List<Role.User> userRoles, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {
        List<DbModels.Spot> spots = [];

        if(userRoles.Contains(Role.User.Admin))
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];
        }
        else if(userRoles.Contains(Role.User.Issuer) && userRoles.Contains(Role.User.Bidder))
        {
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>m.CurrentStatus != Status.Spot.Deleted &&
                    m.UserId == connectedUserId &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];
            // all spot from date range with current user offer 
            spots.AddRange([.. await _session.Query<DbModels.Spot>()
                .Where(m=>(m.CurrentStatus == Status.Spot.Ok ||
                    m.BiddersId!.Contains(connectedUserId)) &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
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
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];
        }
        else if(userRoles.Contains(Role.User.Bidder))
        {
            // all spot from date range with current user offer 
            spots = [.. await _session.Query<DbModels.Spot>()
                .Where(m=>(m.CurrentStatus == Status.Spot.Ok ||
                    m.BiddersId!.Contains(connectedUserId)) &&
                    m.DeadLine >= start && m.DeadLine <= stop)
                .ToAsyncEnumerable(ct)
                .ToListAsync(ct)
            ];
        }        
        
        foreach (var spot in spots)
        {
            var groupsAssignedToSpot = await GetSpotGroups(spot.Id, ct);
            var usersExcluded = await GetUsersExcluded(spot.Id, ct);

            var userSubscibed = _spotChannelManager.GetUserSubscriptionData(connectedUserId);
            List<DbModels.Offer>? offers = null;

            if(connectedUserId == spot.UserId)
            {
                offers = [.. await _session.Query<DbModels.Offer>()
                    .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
                    .ToAsyncEnumerable(ct)
                    .ToListAsync(ct)
                ];
            }
            else
            {
                offers = [.. await _session.Query<DbModels.Offer>()
                    .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted && m.UserId == connectedUserId)
                    .ToAsyncEnumerable(ct)
                    .ToListAsync(ct)
                ];   
            }
            
            var spotModel = new SpotModel(spot, offers, groupsAssignedToSpot, usersExcluded);
            
            _spotDistribution.SendSpotCreatedToUser(spotModel, userSubscibed);
        }
    }
}