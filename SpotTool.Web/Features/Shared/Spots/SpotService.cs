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

    public async Task InitSpotsAsync(Guid connectedUserId, Role.User userRole, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {
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
            var groupsAssignedToSpot = await GetSpotGroups(spot.Id, ct);
            var userSubscibed = _spotChannelManager.GetUserSubscriptionData(connectedUserId);
            _spotDistribution.SendSpotDataToUserAsync(spot, userSubscibed, groupsAssignedToSpot);
        }
    }
}