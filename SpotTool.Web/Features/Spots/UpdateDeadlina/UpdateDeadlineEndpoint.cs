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

public record Request(DateTimeOffset SpotDeadline);


public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.SpotDeadline).GreaterThan(DateTimeOffset.UtcNow).WithMessage("New deadlina must be in a future");
    }
}

public class UpdateDeadlinaEndpoint : Endpoint<Request>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/update-deadlina");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        
        Guid spotId = Route<Guid>("Id");
        
        var updateSpotDeadlineResult = await new UpdateDeadlineCommand(spotId, req.SpotDeadline).ExecuteAsync(ct);
        if(updateSpotDeadlineResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: updateSpotDeadlineResult.ErrorMsg,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Deadline cannot be updated"
            ));
            
            return;
        }

        await Send.NoContentAsync(ct);
    }

    
    
}