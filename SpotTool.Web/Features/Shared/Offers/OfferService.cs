using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Features.Shared.Offers;

public class OfferService(IQuerySession _session)
{
    public async Task<List<DbModels.Offer>> GetAllOffersForSpot(DbModels.Spot spot, CancellationToken ct)
    {
        return [.. await _session.Query<DbModels.Offer>()
                .Where(m => m.SpotId == spot.Id &&
                        m.Status != Status.Offer.Deleted)
                .ToListAsync(ct)
            ];
    }
    public async Task<List<DbModels.Offer>> GetLastOffersForSpotAsync(DbModels.Spot spot, CancellationToken ct)
    {
        if(spot.BiddersId is null)
            return [];

        //var allBidderIds = spot.BiddersId ?? Enumerable.Empty<Guid>(); //Selec(s => s.BiddersId ?? Enumerable.Empty<Guid>()).Distinct().ToArray();

        // ========================================================================
        // 2. JEDNE SZYBKIE ZAPYTANIE PO LEKKIE METADANE (Wszystkie spoty na raz)
        // ========================================================================
        var allOffersMetadata = await _session.Query<DbModels.Offer>()
            .Where(m => m.SpotId == spot.Id && 
                    spot.BiddersId!.Contains(m.CreatedForUser.Id) &&
                    m.Status != Status.Offer.Deleted)
            .Select(m => new { m.SpotId, BidderId = m.CreatedByUser.Id, OfferId = m.Id, m.CreatedAt })
            .ToListAsync(ct);

        // ========================================================================
        // 3. FILTROWANIE "NAJNOWSZYCH" W PAMIĘCI RAM APLIKACJI
        // ========================================================================
        var latestOfferIds = allOffersMetadata
            .OrderByDescending(x => x.CreatedAt)
            // Tworzymy unikalny klucz: kombinacja Spot + Przewoźnik. 
            // Dzięki OrderByDescending, DistinctBy weźmie tylko PIERWSZĄ (najnowszą) ofertę dla tej pary.
            .DistinctBy(x => new { x.SpotId, x.BidderId })
            .Select(x => x.OfferId)
            .ToArray();

        // ========================================================================
        // 4. POBRANIE PEŁNYCH DOKUMENTÓW (Tylko te oferty, które faktycznie pokażemy)
        // ========================================================================
        return [.. await _session.LoadManyAsync<DbModels.Offer>(ct, latestOfferIds)];
    }

    public async Task<Dictionary<Guid, List<DbModels.Offer>>> GetLastOffersForSpotsListAsync(List<DbModels.Spot> spots, CancellationToken ct)
    {
        // ========================================================================
        // 1. PRZYGOTOWANIE PARAMETRÓW DO JEDNEGO ZAPYTANIA (HURTOWO)
        // ========================================================================
        
        var allSpotIds = spots.Select(s => s.Id).ToArray();
        var allBidderIds = spots.SelectMany(s => s.BiddersId ?? Enumerable.Empty<Guid>()).Distinct().ToArray();
        
        if(allBidderIds.IsEmpty())
            return [];
        // ========================================================================
        // 2. JEDNE SZYBKIE ZAPYTANIE PO LEKKIE METADANE (Wszystkie spoty na raz)
        // ========================================================================
        var allOffersMetadata = await _session.Query<DbModels.Offer>()
            .Where(m => allSpotIds.Contains(m.SpotId) 
                    && allBidderIds.Contains(m.CreatedForUser.Id) 
                    && m.Status != Status.Offer.Deleted)
            .Select(m => new { m.SpotId, BidderId = m.CreatedByUser.Id, OfferId = m.Id, m.CreatedAt })
            .ToListAsync(ct);

        // ========================================================================
        // 3. FILTROWANIE "NAJNOWSZYCH" W PAMIĘCI RAM APLIKACJI
        // ========================================================================
        var latestOfferIds = allOffersMetadata
            .OrderByDescending(x => x.CreatedAt)
            // Tworzymy unikalny klucz: kombinacja Spot + Przewoźnik. 
            // Dzięki OrderByDescending, DistinctBy weźmie tylko PIERWSZĄ (najnowszą) ofertę dla tej pary.
            .DistinctBy(x => new { x.SpotId, x.BidderId })
            .Select(x => x.OfferId)
            .ToArray();

        // ========================================================================
        // 4. POBRANIE PEŁNYCH DOKUMENTÓW (Tylko te oferty, które faktycznie pokażemy)
        // ========================================================================
        var fullOffers = await _session.LoadManyAsync<DbModels.Offer>(ct, latestOfferIds);

        // Grupujemy pobrane pełne oferty po SpotId, aby w pętli foreach mieć do nich błyskawiczny dostęp O(1)
        return fullOffers
            .GroupBy(o => o.SpotId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}