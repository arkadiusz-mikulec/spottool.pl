using System.Threading.Channels;
using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;

namespace SpotTool.Web.Features.Spots.UpdateDeadlina;

public record Request(Guid SpotId, DateTimeOffset SpotDeadline);


public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.SpotDeadline).GreaterThan(DateTimeOffset.UtcNow).WithMessage("New deadlina must be in a future");
    }
}

public class UpdateDeadlinaEndpoint(
    IDocumentSession _session, 
    SpotChannelManager _spotChannelManager,
    Channel<Guid> _updateChannel,
    IDateTimeProvider _dateTimeProvider) : Endpoint<Request>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Patch("/api/v1/integrations/spot/update-deadlina");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        

        var spot = await _session.LoadAsync<DbModels.Spot>(req.SpotId, ct);
        if(spot is null)
        {
            await Send.NotFoundAsync(ct);
            return; //Task.FromResult(new ProblemDetails();
        }
        
        if(!CanBeUpdated(spot))
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: "Spot deadlinie cannot be updated becasue spot is in wrong status",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Spot deadline cannot be updated"
            ));
            
            return;
        }
        
        spot.DeadLine = _dateTimeProvider.FormatDateTimeWithoutSecunds(req.SpotDeadline);

        _session.Store(spot);
                           
        await _session.SaveChangesAsync(ct);
        await _spotChannelManager.DistributeSpotAsync(spot, ct);
        await _updateChannel.Writer.WriteAsync(spot.Id, ct);
        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.NoContentAsync(ct);
    }

    private static bool CanBeUpdated(DbModels.Spot spot)
    {
        if(spot.CurrentStatus == Status.Spot.NotConfirmed || spot.CurrentStatus == Status.Spot.Ok || spot.CurrentStatus == Status.Spot.DuringBidding)
            return true;
        return false;
    }
    
}