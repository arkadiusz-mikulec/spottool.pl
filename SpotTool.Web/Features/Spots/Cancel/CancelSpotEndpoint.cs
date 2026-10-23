using FastEndpoints;

namespace SpotTool.Web.Features.Spots.Cancel;

public record Request(string? Reason = null);
public record Respond(bool Success, string? Reason);
public class CreateEndpoint : Endpoint<Request, Respond> //EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/cancel");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        Guid spotId = Route<Guid>("Id");

        var cancelSpotCommandResult = await new CancelSpotCommand(spotId, req.Reason).ExecuteAsync(ct);

        if(cancelSpotCommandResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: cancelSpotCommandResult.ErrorMsg,
                statusCode: cancelSpotCommandResult.StatusCode,
                title: "Spot cannot be canceled"
            ));
            
            return;
        }

        await Send.OkAsync(new Respond(true, req.Reason), ct);
    }
}