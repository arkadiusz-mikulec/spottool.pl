using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Offers;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Accept;

public record AcceptSpotRespond(bool IsError, string? ErrorMesage = null);
public record AcceptSpotCommand(Guid SpotId, Guid OfferId) : ICommand<AcceptSpotRespond>;

public class AcceptSpotHandler(
    IDocumentSession _session,
    SpotDistribution _spotDistribution,
    SpotService _spotService,
    OfferService _offerService) : ICommandHandler<AcceptSpotCommand, AcceptSpotRespond>
{
    public async Task<AcceptSpotRespond> ExecuteAsync(AcceptSpotCommand command, CancellationToken ct)
    {
        var spot = await _session.LoadAsync<DbModels.Spot>(command.SpotId, ct);
        if(spot is null)
            return new AcceptSpotRespond(true, "Spot was not found");
        
        if(!CanBeAccepted(spot))
            return new AcceptSpotRespond(true, "Spot cannot be acceped");

        var winnerOffer = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Id == command.OfferId)
            .FirstOrDefaultAsync(ct);

        if(winnerOffer is null)
            return new AcceptSpotRespond(true, "Offer was not found");

        if(!CanOfferBeSelected(winnerOffer))
            return new AcceptSpotRespond(true, "Offer cannot be selected as winner one because is in a wrong status");
        

        winnerOffer.IsWinnerOffer = true;
        _session.Store(winnerOffer);

        spot.CurrentStatus = Status.Spot.Accepted;
        spot.WinnerOfferSnapShot = new DbModels.OfferSnapShot(
            winnerOffer.Id,
            winnerOffer.CreatedByUser,
            winnerOffer.Value,
            winnerOffer.CreatedAt,
            winnerOffer.Status,
            winnerOffer.Remarks,
            winnerOffer.ValidTill,
            winnerOffer.CurrencyCode);    

        _session.Store(spot);
        
        _session.Store(new DbModels.SpotStatusHistory
        {
            SpotId = spot.Id,
            SpotStatus = spot.CurrentStatus
        });                    

        await _session.SaveChangesAsync(ct);

        // rozwiazenie pokazuje tylko ostatnią oferte
        // var lastOffers = await _offerService.GetLastOffersForSpotAsync(spot, ct);
        // var lastOffersIds = lastOffers.Select(m=>m.Id);
        // if(!lastOffersIds.Contains(winnerOffer.Id))
        //     lastOffers.Add(winnerOffer);

        var spotModel = new SpotModel(
            spot, 
            await _offerService.GetAllOffersForSpot(spot, ct), 
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct)
        );
        _ = _spotDistribution.SendSpotStatusToAuthorizedUsers(spotModel);
        
        return new AcceptSpotRespond(false);
    }

    private static bool CanBeAccepted(DbModels.Spot spot)
    {
        return spot.CurrentStatus switch
        {
            Status.Spot.Ended or Status.Spot.Rejected => true,
            _ => false
        };
    }

    private static bool CanOfferBeSelected(DbModels.Offer offer)
    {
        return offer.Status switch
        {
            Status.Offer.Deleted or Status.Offer.ToNegotiation => false,
            _ => true
        };
    }
}
