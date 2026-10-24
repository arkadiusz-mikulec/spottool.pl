using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Offers.Reject;

public record RejectOfferRespond(string? ErrorMsg = null, int StatusCode = StatusCodes.Status404NotFound, bool IsError = true);
public record RejectOfferCommand(Guid OfferId, Guid CreateByUserId, Guid CreatedForUserId, string? Reason = null) : ICommand<RejectOfferRespond>;
public class RejectOfferHandler(IDocumentSession _session,
    SpotDistribution _spotDistribution) : ICommandHandler<RejectOfferCommand, RejectOfferRespond>
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
            _session.Store(spot);
            _session.Store(new DbModels.SpotStatusHistory
            {
                SpotId = spot.Id,
                SpotStatus = Status.Spot.Ended
            });
        }
        else
        {
            offer.Status = Status.Offer.Rejected;

            if(!string.IsNullOrEmpty(command.Reason))
                offer.RejectReson = command.Reason.Trim().ReplaceLineEndings(" ");

            _session.Store(offer);
        }
        
        await _session.SaveChangesAsync(ct);

        var offers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);

        _spotDistribution.SendOfferRejectedToIssuerAndBidder(spot.Id, spot.CurrentStatus, spot.UserId, offer.UserId, offers);

        return new RejectOfferRespond(IsError: false, StatusCode: StatusCodes.Status200OK);
    }
}
