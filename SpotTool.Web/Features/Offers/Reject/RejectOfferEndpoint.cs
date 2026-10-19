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

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(string? Reason);
public record Response(bool Rejected);

public class CreateEndpoint(IDocumentSession _session, 
    SpotDistribution _spotDistributor) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Patch("/api/v1/integrations/offers/{Id}/reject");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        Guid offerId = Route<Guid>("Id");

        var offer = await _session.Query<DbModels.Offer>()
            .Where(m=>m.Id == offerId && m.Status != Status.Offer.Deleted)
            .FirstOrDefaultAsync(ct);

        if(offer is null)
        {
            await Send.NotFoundAsync(ct);
            return; //Task.FromResult(new ProblemDetails();
        }
        
        offer.Status = Status.Offer.Rejected;
        if(!string.IsNullOrEmpty(req.Reason))
            offer.RejectReson = req.Reason.Trim().ReplaceLineEndings(" ");

        _session.Store(offer);
        
        await _session.SaveChangesAsync(ct);

        var spot = await _session.LoadAsync<DbModels.Spot>(offer.SpotId, ct);
        if(spot is null)
        {
            await Send.NotFoundAsync(ct);
            return; //Task.FromResult(new ProblemDetails();
        }

        var offers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);

        _spotDistributor.SendOfferRejectedToIssuerAndBidder(spot.Id, spot.CurrentStatus, spot.UserId, offer.UserId, offers);
    
        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(true), ct);
    }
}