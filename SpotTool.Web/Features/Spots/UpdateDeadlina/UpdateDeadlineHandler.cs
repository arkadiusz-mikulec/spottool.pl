using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Offers;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.UpdateDeadlina;

public record UpdateDeadlineRespond(string? ErrorMsg = null, bool IsError = true);
public record UpdateDeadlineCommand(Guid SpotId, DateTimeOffset Deadline): ICommand<UpdateDeadlineRespond>;

public class UpdateDeadlineHandler(IDocumentSession _session,
    SpotDistribution _spotDistribution,
    Channel<Guid> _updateChannel,
    SpotService _spotService,
    IDateTimeProvider _dateTimeProvider,
    OfferService _offerService) : ICommandHandler<UpdateDeadlineCommand, UpdateDeadlineRespond>
{
    public async Task<UpdateDeadlineRespond> ExecuteAsync(UpdateDeadlineCommand command, CancellationToken ct)
    {
        var spot = await _session.LoadAsync<DbModels.Spot>(command.SpotId, ct);
        if(spot is null)
            return new UpdateDeadlineRespond("Spot was not found");
        
        if(!CanBeUpdated(spot))
            return new UpdateDeadlineRespond("Spot deadlinie cannot be updated becasue spot is in wrong status");
        
        spot.DeadLine = _dateTimeProvider.FormatDateTimeWithoutSecunds(command.Deadline);

        _session.Store(spot);
                           
        await _session.SaveChangesAsync(ct);

        var spotModel = new SpotModel(
            spot, 
            await _offerService.GetLastOffersForSpotAsync(spot, ct),
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct)
        );
        _ = _spotDistribution.SendSpotDeadlineUpdateToAuthorizedUsers(spotModel);
        _updateChannel.Writer.TryWrite(spot.Id);

        return new UpdateDeadlineRespond(IsError: false);
    }

    private static bool CanBeUpdated(DbModels.Spot spot)
    {
        if(spot.CurrentStatus == Status.Spot.NotConfirmed || 
            spot.CurrentStatus == Status.Spot.Ok || 
            spot.CurrentStatus == Status.Spot.Bidding
        )
            return true;
        return false;
    }
}