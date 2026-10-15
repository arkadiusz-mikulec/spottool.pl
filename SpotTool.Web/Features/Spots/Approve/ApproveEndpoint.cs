using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Approve;

public record Response(bool Approved);

public class ApproveEndpoint(
    IDocumentSession _session, 
    SpotDistribution _spotDistribution,
    Channel<Guid> _wakeupChannel,
    IDateTimeProvider _dateTimeProvider,
    SpotService _spotService) : EndpointWithoutRequest<Response>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/approve");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        var id = Route<Guid>("Id");
        //Console.WriteLine($"ID: {id}");
        var spot = await _session.LoadAsync<DbModels.Spot>(id, ct);
        if(spot is null)
        {
            await Send.NotFoundAsync(ct);
            return; //Task.FromResult(new ProblemDetails();
        }
        
        if(!CanBeApproved(spot, _dateTimeProvider.Now()))
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: "Spot cannot be approved becasue is after deadlina or in wrogn status",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Spot cannot be approved"
            ));
            
            return;
        }

        
        spot.CurrentStatus = Status.Spot.Ok;    
        _session.Store(spot);
        
        _session.Store(new DbModels.SpotStatusHistory
        {
            SpotId = id,
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
        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(true), ct);
    }

    private static bool CanBeApproved(DbModels.Spot spot, DateTimeOffset currentDateTime)
    {
        Console.WriteLine($"DL: {spot.DeadLine}, CT: {currentDateTime}, DL Is bigger {spot.DeadLine >= currentDateTime}");
        if(spot.DeadLine >= currentDateTime && spot.CurrentStatus == Status.Spot.NotConfirmed)
            return true;
        return false;
    }
    
}