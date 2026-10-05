using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;

namespace SpotTool.Web.Features.Spots.Approve;

public record Response(bool Approved);

public class ApproveEndpoint(
    IDocumentSession _session, 
    SpotChannelManager _spotChannelMenager,
    Channel<Guid> _wakeupChannel,
    IDateTimeProvider _dateTimeProvider) : EndpointWithoutRequest<Response>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Post("/api/v1/integrations/spot/{Id}/approve");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        var id = Route<Guid>("Id");

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

        await _spotChannelMenager.DistributeSpotAsync(spot, ct);
        await _wakeupChannel.Writer.WriteAsync(spot.Id, ct);
        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(true), ct);
    }

    private static bool CanBeApproved(DbModels.Spot spot, DateTimeOffset currentDateTime)
    {
        if(spot.DeadLine >= currentDateTime && spot.CurrentStatus == Status.Spot.NotConfirmed)
            return true;
        return false;
    }
    
}