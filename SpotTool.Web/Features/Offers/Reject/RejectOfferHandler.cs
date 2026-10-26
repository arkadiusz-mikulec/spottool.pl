using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Offers.Reject;

public record RejectOfferRespond(string? ErrorMsg = null, int StatusCode = StatusCodes.Status404NotFound, bool IsError = true);
public record RejectOfferCommand(Guid OfferId, Guid CreateByUserId, Guid CreatedForUserId, string? Reason = null) : ICommand<RejectOfferRespond>;
public class RejectOfferHandler(IDocumentSession _session,
    SpotDistribution _spotDistribution,
    SpotService _spotService) : ICommandHandler<RejectOfferCommand, RejectOfferRespond>
{
    public async Task<RejectOfferRespond> ExecuteAsync(RejectOfferCommand command, CancellationToken ct)
    {
        var offer = await _session.Query<DbModels.Offer>()
            .Where(m=>m.Id == command.OfferId && m.Status != Status.Offer.Deleted)
            .FirstOrDefaultAsync(ct);

        if(offer is null)
            return new RejectOfferRespond("Offer not found");

        var spot = await _session.LoadAsync<DbModels.Spot>(offer.SpotId, ct);
        if(spot is null)
            return new RejectOfferRespond("Spot not found", StatusCodes.Status500InternalServerError);
   
        //If not equal menas that it it negotiation offer
        if(!command.CreateByUserId.Equals(command.CreatedForUserId))
        {
            spot.CurrentStatus = Status.Spot.Ended;

            //SetOferStatusRejectedAndReson(offer, command.Reason);

            _session.Store(spot);
            _session.Store(new DbModels.SpotStatusHistory
            {
                SpotId = spot.Id,
                SpotStatus = Status.Spot.Ended
            });
        }
        else
            SetOferStatusRejectedAndReson(offer, command.Reason);
            
        _session.Store(offer);
        await _session.SaveChangesAsync(ct);

        var offers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);

        SpotModel spotModel = new (spot, offers, 
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct));
            
        _spotDistribution.SendOfferRejectedToIssuerAndBidder(spotModel, offer);

        return new RejectOfferRespond(IsError: false, StatusCode: StatusCodes.Status200OK);
    }

    private static void SetOferStatusRejectedAndReson(DbModels.Offer offer, string? reason)
    {
        offer.Status = Status.Offer.Rejected;

        if(!string.IsNullOrEmpty(reason))
            offer.RejectReson = reason.Trim().ReplaceLineEndings(" ");
    }
}
