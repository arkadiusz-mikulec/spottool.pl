using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Cancel;

public record CancelSpotRespond(string? ErrorMsg = null, int StatusCode = StatusCodes.Status404NotFound, bool IsError = true);
public record CancelSpotCommand(Guid SpotId, string? Reason): ICommand<CancelSpotRespond>;

public class CancelSpotHandler(IDocumentSession _session,
    SpotDistribution _spotDistribution,
    SpotService _spotService) : ICommandHandler<CancelSpotCommand, CancelSpotRespond>
{
    public async Task<CancelSpotRespond> ExecuteAsync(CancelSpotCommand command, CancellationToken ct)
    {
        var spot = await _session.LoadAsync<DbModels.Spot>(command.SpotId, ct);

        if(spot is null)
            return new CancelSpotRespond("Spot was not found");
        
        if(!CanBeCanceled(spot.CurrentStatus))
            return new CancelSpotRespond("Spot cannot be canceled!", StatusCodes.Status400BadRequest);

        spot.CurrentStatus = spot.BiddersId?.Count > 0 ? Status.Spot.Canceled : Status.Spot.Deleted;
        spot.CancelReason = command.Reason?.Trim().ReplaceLineEndings(" ");

        _session.Store(spot);
        _session.Store(new DbModels.SpotStatusHistory
        {
            SpotId = command.SpotId,
            SpotStatus = spot.CurrentStatus
        }); 

        await _session.SaveChangesAsync(ct);

        var spotOffers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .OrderBy(m=>m.Value)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);

        var spotModel = new SpotModel(
            spot, 
            spotOffers,
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct) 
        );

        _ = _spotDistribution.SendSpotStatusToAuthorizedUsers(spotModel);

        return new CancelSpotRespond(IsError: false, StatusCode: StatusCodes.Status200OK);
    }

    private static bool CanBeCanceled(Status.Spot spotStatus)
    {
        if(spotStatus != Status.Spot.Canceled && spotStatus != Status.Spot.Deleted)
            return true;
        return false;
    }
}