using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;
using SpotTool.Web.Features.Spots.Accept;

namespace SpotTool.Web.Features.Spots.Approve;


public record ApproveSpotRespond(string? ErrorMsg = null, bool IsError = true);
public record ApproveSpotRequest(Guid SpotId): ICommand<ApproveSpotRespond>;
public class ApproveSpotHandler(IDocumentSession _session,
    IDateTimeProvider _dateTimeProvider,
    SpotService _spotService,
    SpotDistribution _spotDistribution,
    Channel<Guid> _wakeupChannel) : ICommandHandler<ApproveSpotRequest, ApproveSpotRespond>
{
    public async Task<ApproveSpotRespond> ExecuteAsync(ApproveSpotRequest command, CancellationToken ct)
    {
        var spot = await _session.LoadAsync<DbModels.Spot>(command.SpotId, ct);
        if(spot is null)
            return new ApproveSpotRespond("Spot not found");
        
        if(!CanBeApproved(spot, _dateTimeProvider.Now()))
            return new ApproveSpotRespond("Spot cannot be approved becasue is after deadlina or in wrogn status");
    
        
        spot.CurrentStatus = Status.Spot.Ok;    
        _session.Store(spot);
        
        _session.Store(new DbModels.SpotStatusHistory
        {
            SpotId = command.SpotId,
            SpotStatus = Status.Spot.Ok
        });                    

        await _session.SaveChangesAsync(ct);

        var spotModel = new SpotModel(
            spot, 
            null, 
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct)
        );
        _ = _spotDistribution.SendSpotApproveToAuthorizedUsers(spotModel);
        _wakeupChannel.Writer.TryWrite(spot.Id);

        return new ApproveSpotRespond(IsError: false);
    }

    private static bool CanBeApproved(DbModels.Spot spot, DateTimeOffset currentDateTime)
    {
        if(spot.DeadLine >= currentDateTime && spot.CurrentStatus == Status.Spot.NotConfirmed)
            return true;
        return false;
    }
}