using FastEndpoints;
using FluentValidation;
using JasperFx.CodeGeneration.Frames;
using Marten;
using Marten.Patching;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;


namespace SpotTool.Web.Features.Offers.Reject;

public record Request(Guid CreatedByUserId, Guid CreatedForUserId, string? Reason = null);
public record Response(bool Rejected, string? Reason = null);

public class CreateEndpoint : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Patch("/api/v1/integrations/offers/{Id}/reject");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        Guid offerId = Route<Guid>("Id");

        var rejectOfferCommandResult = await new RejectOfferCommand(offerId, req.CreatedByUserId, req.CreatedForUserId, req.Reason)
            .ExecuteAsync(ct);

        if(rejectOfferCommandResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: rejectOfferCommandResult.ErrorMsg,
                statusCode: rejectOfferCommandResult.StatusCode,
                title: "Offer cannot be rejected"
            ));
            
            return;
        }
           
        await Send.OkAsync(new Response(true, req.Reason), ct);
    }
}