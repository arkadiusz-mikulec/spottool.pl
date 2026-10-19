using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Reject;

public record Response(bool Rejected);

public class RejectEndpoint : EndpointWithoutRequest<Response>
{ 
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/reject");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        var id = Route<Guid>("Id");
        //Console.WriteLine($"ID: {id}");
        var rejectCommandResult = await new RejectSpotCommand(id).ExecuteAsync(ct);
        if(rejectCommandResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: rejectCommandResult.ErrorMsg,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Spot cannot be rejected"
            ));
            
            return;
        }    
        await Send.OkAsync(new Response(true), ct);
    }
}