using FastExpressionCompiler.ImTools;
using Marten;
using SpotTool.Web.Contracts;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Offers;
using SpotTool.Web.Features.Users;


namespace SpotTool.Web.Features.Shared.Spots;

//docelowo to bedzie in memory casch
public class SpotService(
    IQuerySession _querySession, 
    IDateTimeProvider _dateTimeProvider,
    OfferService _offerService,
    UserService _userService)
{
    // public async Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct)
    // {
    //     //using var session = _store.QuerySession();
    //     var spot = await _querySession.LoadAsync<DbModels.Spot>(id, ct);

    //     if (spot is null)
    //         return null;
    //     return spot;
    // }

    // public async Task<List<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    // {
    //     //using var session = _store.QuerySession();
    //     return await _querySession.Query<DbModels.Spot>().ToAsyncEnumerable(ct).ToListAsync(ct);
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
        List<DbModels.Spot> spots = [.. await _querySession.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok ||
                m.CurrentStatus == Status.Spot.Bidding ||
                m.CurrentStatus == Status.Spot.NotConfirmed ||
                m.CurrentStatus == Status.Spot.Negotiation ||
                m.CurrentStatus == Status.Spot.Ended)
            .ToListAsync(ct)
        ];

        spots.AddRange(await GetEndedSpots(connectedUserId, today, today.AddDays(1), ct));

        //Tmp solition to not duble position in spot list
        // spots.AddRange([.. await _querySession.Query<DbModels.Spot>()
        //     .Where(m=>m.BiddersId!.Contains(connectedUserId) &&
        //         m.DeadLine >= today && m.DeadLine < today.AddDays(1))
        //     .ToListAsync(ct)
        // ]);
            
        return await InitSpotUiRespond2(spots, connectedUserId, ct);
    }

    public async Task<List<SpotUIRespond>> InitHistoricalSpotsAsync(Guid connectedUserId, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {   
        List<DbModels.Spot> spots = await GetEndedSpots(connectedUserId, start, stop, ct);

        spots.AddRange([.. await _querySession.Query<DbModels.Spot>()
            .Where(m=>m.BiddersId!.Contains(connectedUserId) &&
                m.DeadLine >= start && m.DeadLine < stop)
            .ToListAsync(ct)
        ]);

        return await InitSpotUiRespond(spots, connectedUserId, ct);
    }

    private async Task<List<DbModels.Spot>> GetEndedSpots(Guid userId, DateTimeOffset start, DateTimeOffset stop, CancellationToken ct)
    {
        //Tymczasowo usuniete m.UserId == userId && zeby pokazywac spoty
        return [.. await _querySession.Query<DbModels.Spot>()
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
            // List<DbModels.Offer>? offers = [.. await _querySession.Query<DbModels.Offer>()
            //         .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            //         .OrderBy(m=>m.CreatedAt)
            //         .ToListAsync(ct)
            //     ];
            
            // offers = [.. offers.DistinctBy(m=>m.CreatedForUser.Id)];

            // !!!!!!  Kod generuje ultra-wydajny, natywny SQL bezpośrednio w PostgreSQL
            // var lastOffers = await _querySession.QueryAsync<DbModels.Offer>(
            //     @"SELECT DISTINCT ON (data ->> 'CreatedForUserId') * 
            //     FROM mt_doc_offer 
            //     WHERE (data ->> 'SpotId') = :spotId 
            //         AND (data ->> 'Status') != :deletedStatus
            //     ORDER BY data ->> 'CreatedForUserId', data ->> 'CreatedAt'",
            //     new { 
            //         spotId = spot.Id.ToString(), 
            //         deletedStatus = Status.Offer.Deleted.ToString() 
            //     }, 
            //     ct);

            //Alternaltywa
            // 1. Pobieramy z bazy tylko niezbędne, lekkie metadane (zamiast całych grubych dokumentów)
            var offerMetadata = await _querySession.Query<DbModels.Offer>()
                .Where(m => m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
                .Select(m => new { CarrierId = m.CreatedForUser.Id, OfferId = m.Id, m.CreatedAt })
                .ToListAsync(ct); // Marten bez problemu radzi sobie z taką prostą projekcją

            // 2. Filtrujemy w pamięci aplikacji (operacja na bardzo lekkiej liście w RAM-ie)
            var latestOfferIds = offerMetadata
                .OrderBy(x => x.CreatedAt)
                .DistinctBy(x => x.CarrierId)
                .Select(x => x.OfferId)
                .ToArray();

            // 3. Ładujemy z bazy pełne, gotowe dokumenty tylko dla tych konkretnych, najnowszych ofert
            var lastOffers = await _querySession.LoadManyAsync<DbModels.Offer>(ct, latestOfferIds);

            Role.User userRole = _userService.IsSpotCreator(connectedUserId, spot.UserId) ? Role.User.Issuer : Role.User.Bidder;
            var spotModel = new SpotModel(
                spot, 
                [.. lastOffers], 
                await GetSpotGroups(spot.Id, ct), 
                await GetUsersExcluded(spot.Id, ct)
            );
            spotUIs.Add(new SpotUIRespond(spotModel, connectedUserId, userRole));
        }
        return [.. spotUIs.OrderBy(m=>m.SpotModel.Spot.DeadLine)];
    }

    private async Task<List<SpotUIRespond>> InitSpotUiRespond2(List<DbModels.Spot> spots, Guid connectedUserId, CancellationToken ct)
    {
        List<SpotUIRespond> spotUIs = [];

        var offersGroupedBySpot = await _offerService.GetLastOffersForSpotsListAsync(spots, ct);

        foreach (var spot in spots)
        {
            SpotModel spotModel;
            if(!offersGroupedBySpot.IsEmpty() && offersGroupedBySpot.TryGetValue(spot.Id, out var lastOffersForSpot))
                spotModel = new SpotModel(
                    spot, 
                    lastOffersForSpot,  
                    await GetSpotGroups(spot.Id, ct), 
                    await GetUsersExcluded(spot.Id, ct)
                );
            else
                spotModel = new SpotModel(
                    spot, 
                    [], 
                    await GetSpotGroups(spot.Id, ct), 
                    await GetUsersExcluded(spot.Id, ct)
                );
            
            Role.User userRole = _userService.IsSpotCreator(connectedUserId, spot.UserId) ? Role.User.Issuer : Role.User.Bidder;
            spotUIs.Add(new SpotUIRespond(spotModel, connectedUserId, userRole));
        }
        return [.. spotUIs.OrderBy(m=>m.SpotModel.Spot.DeadLine)];
    }
}