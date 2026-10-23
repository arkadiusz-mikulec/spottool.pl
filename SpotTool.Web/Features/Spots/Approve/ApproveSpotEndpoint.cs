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

public class ApproveEndpoint : EndpointWithoutRequest<Response>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/approve");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("Id");
        
        var approveSpotCommandResult = await new ApproveSpotRequest(id).ExecuteAsync(ct);
        if(approveSpotCommandResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: approveSpotCommandResult.ErrorMsg,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Spot cannot be approved"
            ));
            
            return;
        }
        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(true), ct);
    }

    
    
}