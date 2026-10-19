using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Accept;

public record Request(Guid OfferId);
public record Response(bool Accepted);

public class AcceptEndpoint : Endpoint<Request, Response>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/accept");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        var id = Route<Guid>("Id");

        var acceptCommandResult = await new AcceptSpotCommand(id, req.OfferId).ExecuteAsync(ct);
        if(acceptCommandResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: acceptCommandResult.ErrorMesage,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Spot cannot be accepted"
            ));
            
            return;
        } 
        await Send.OkAsync(new Response(true), ct);
    }
    
}