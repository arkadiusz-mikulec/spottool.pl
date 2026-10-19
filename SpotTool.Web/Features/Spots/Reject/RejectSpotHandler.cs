using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Reject;

public record RejectSpotRespond(string? ErrorMsg = null, bool IsError = true);
public record RejectSpotCommand(Guid SpotId) : ICommand<RejectSpotRespond>;
public class RejectSpotHandler(
    IDocumentSession _session,
    SpotDistribution _spotDistribution,
    SpotService _spotService) : ICommandHandler<RejectSpotCommand, RejectSpotRespond>
{
    public async Task<RejectSpotRespond> ExecuteAsync(RejectSpotCommand command, CancellationToken ct)
    {
        var spot = await _session.LoadAsync<DbModels.Spot>(command.SpotId, ct);
        if(spot is null)
            return new RejectSpotRespond("Spot cannot be found");

        
        if(!CanBeRejected(spot))
            return new RejectSpotRespond("Spot cannot be rejected");

        if(spot.CurrentStatus == Status.Spot.Accepted && spot.WinnerOfferSnapShot is not null)
        {
            var offerToRejecte = await _session.LoadAsync<DbModels.Offer>(spot.WinnerOfferSnapShot.Id, ct);
            if(offerToRejecte is null)
                return new RejectSpotRespond("Spot cannot be rejected, because prev winner offer cannot be found");

            offerToRejecte.IsWinnerOffer = false;
            _session.Store(offerToRejecte);
        }
        
        spot.CurrentStatus = Status.Spot.Rejected;
        spot.WinnerOfferSnapShot = null;    

        _session.Store(spot);
        
        _session.Store(new DbModels.SpotStatusHistory
        {
            SpotId = spot.Id,
            SpotStatus = spot.CurrentStatus
        });                    

        await _session.SaveChangesAsync(ct);

        var offers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);

        var spotModel = new SpotModel(
            spot, 
            offers, 
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct)
        );
        _ = _spotDistribution.SendSpotStatusToAuthorizedUsers(spotModel);

        return new RejectSpotRespond(IsError: false);
    }

    private static bool CanBeRejected(DbModels.Spot spot)
    {
        return spot.CurrentStatus switch
        {
            Status.Spot.Accepted or Status.Spot.Ended => true,
            _ => false,
        };
    }
}