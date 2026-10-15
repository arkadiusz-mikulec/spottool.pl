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


namespace SpotTool.Web.Features.Spots.Cancel;

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(string? Reason);
public record Response(bool Canceled);

public class CreateEndpoint(IDocumentSession _session, 
    SpotDistribution _spotDistributor,
    SpotService _spotService) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Patch("/api/v1/integrations/spots/{Id}/cancel");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        Guid spotId = Route<Guid>("Id");

        var spot = await _session.Query<DbModels.Spot>()
            .Where(m=>m.Id == spotId && m.CurrentStatus != Status.Spot.Canceled && m.CurrentStatus != Status.Spot.Deleted)
            .FirstOrDefaultAsync(ct);

        if(spot is null)
        {
            await Send.NotFoundAsync(ct);
            return; //Task.FromResult(new ProblemDetails();
        }
        
        spot.CurrentStatus = spot.BiddersId?.Count > 0 ? Status.Spot.Canceled : Status.Spot.Deleted;
        spot.CancelReason = req.Reason?.Trim().ReplaceLineEndings(" ");

        _session.Store(spot);
        _session.Store(new DbModels.SpotStatusHistory
        {
            SpotId = spotId,
            SpotStatus = spot.CurrentStatus
        }); 

        await _session.SaveChangesAsync(ct);

        var spotOffers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .OrderBy(m=>m.Value)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);

        var spotModel = new SpotModel(
            spot, 
            spotOffers,
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct) 
        );

        _ = _spotDistributor.SendSpotStatusToAuthorizedUsers(spotModel);

        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(true), ct);
    }
}