using FastExpressionCompiler.ImTools;
using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;


namespace SpotTool.Web.Features.Shared.Spots;

//docelowo to bedzie in memory casch
public class SpotService(IDocumentSession _session, IDateTimeProvider _dateTimeProvider)
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
    public async Task<List<SpotUIRespond>> InitActiveSpotsAsync(Guid connectedUserId, TimeSpan offset, CancellationToken ct)
    {   
        DateTime today = _dateTimeProvider.TodayInUserLocalTime(offset);
        
        //Tymczasowo usunolem userId == connectedUserId!! Warunki sa zle nie pokazuja spotów dla Bidderów
        List<DbModels.Spot> spots = [.. await _session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok ||
                m.CurrentStatus == Status.Spot.Bidding ||
                m.CurrentStatus == Status.Spot.NotConfirmed ||
                m.CurrentStatus == Status.Spot.Negotiation ||
                m.CurrentStatus == Status.Spot.Ended)
            .ToListAsync(ct)
        ];

        spots.AddRange(await GetEndedSpots(connectedUserId, today, today.AddDays(1), ct));

        //Tmp solition to not duble position in spot list
        // spots.AddRange([.. await _session.Query<DbModels.Spot>()
        //     .Where(m=>m.BiddersId!.Contains(connectedUserId) &&
        //         m.DeadLine >= today && m.DeadLine < today.AddDays(1))
        //     .ToListAsync(ct)
        // ]);
            
        return await InitSpotUiRespond(spots, connectedUserId, ct);
    }

    public async Task<List<SpotUIRespond>> InitHistoricalSpotsAsync(Guid connectedUserId, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {   
        List<DbModels.Spot> spots = await GetEndedSpots(connectedUserId, start, stop, ct);

        spots.AddRange([.. await _session.Query<DbModels.Spot>()
            .Where(m=>m.BiddersId!.Contains(connectedUserId) &&
                m.DeadLine >= start && m.DeadLine < stop)
            .ToListAsync(ct)
        ]);

        return await InitSpotUiRespond(spots, connectedUserId, ct);
    }

    private async Task<List<DbModels.Spot>> GetEndedSpots(Guid userId, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {
        //Tymczasowo usuniete m.UserId == userId && zeby pokazywac spoty
        return [.. await _session.Query<DbModels.Spot>()
                .Where(m=>(m.CurrentStatus == Status.Spot.EndedNoOffer ||
                    m.CurrentStatus == Status.Spot.Accepted ||
                    m.CurrentStatus == Status.Spot.Canceled ||
                    m.CurrentStatus == Status.Spot.Rejected) &&
                    m.DeadLine >= start && m.DeadLine < stop)
                .ToListAsync(ct)
            ];
    }

    private async Task<List<SpotUIRespond>> InitSpotUiRespond(List<DbModels.Spot> spots, Guid connectedUserId, CancellationToken ct)
    {
        List<SpotUIRespond> spotUIs = [];
        foreach (var spot in spots)
        {
            List<DbModels.Offer>? offers = [.. await _session.Query<DbModels.Offer>()
                    .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
                    .ToListAsync(ct)
                ];

            Role.User userRole = IsSpotCreator(connectedUserId, spot.UserId) ? Role.User.Issuer : Role.User.Bidder;
            var spotModel = new SpotModel(spot, offers, null, null);
            spotUIs.Add(new SpotUIRespond(spotModel, connectedUserId, userRole));
        }
        return [.. spotUIs.OrderBy(m=>m.SpotModel.Spot.DeadLine)];
    }
    private static bool IsSpotCreator(Guid? userId, Guid spotUserId)
    {
        //tutaj bedzie cala logika dotyczaca BU!
        return spotUserId.Equals(userId);
    }
    // private static bool CarrierHasAccess(Guid userId, List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups, List<Guid> usersExcluded)
    // {
    //     bool isGlobal = groupsAssignedToSpot.Count == 0; 
    //     return isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any() || !usersExcluded.Contains(userId);    
    // }
}