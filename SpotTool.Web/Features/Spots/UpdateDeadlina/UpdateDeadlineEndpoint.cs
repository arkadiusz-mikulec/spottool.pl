using System.Threading.Channels;
using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;

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
    SpotDistribution _spotDistributor,
    Channel<Guid> _updateChannel,
    IDateTimeProvider _dateTimeProvider,
    SpotService _spotService) : Endpoint<Request>
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

        var spotModel = new SpotModel(spot, null,
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct)
        );
        _ = _spotDistributor.SendSpotDeadlineUpdateToAuthorizedUsers(spotModel);
        _updateChannel.Writer.TryWrite(spot.Id);
        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.NoContentAsync(ct);
    }

    private static bool CanBeUpdated(DbModels.Spot spot)
    {
        if(spot.CurrentStatus == Status.Spot.NotConfirmed || spot.CurrentStatus == Status.Spot.Ok || spot.CurrentStatus == Status.Spot.Bidding)
            return true;
        return false;
    }
    
}