using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Db;
using SpotTool.Web.Features.Shared;


namespace SpotTool.Web.Features.Offers.Add;

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(Guid SpotId, Guid UserId, decimal Value, DateTimeOffset ValidTill, DbModels.UserSnapShot CreateBy);
public record Response(Guid OfferId);

// 2. Automatyczny walidator (FastEndpoints odpala go sam!)
public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ValidTill).GreaterThan(DateTimeOffset.Now).WithMessage("Oferta musi być ważna dłużej niż NOW!");
        RuleFor(x => x.Value).GreaterThan(0).WithMessage("Wartość oferty musi być dodatnia!");
    }
}

// 3. Sam Endpoint + Handler w jednym miejscu
public class AddEndpoint(IDocumentStore store) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI

    public override void Configure()
    {
        Post("/api/v1/integrations/offers");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        Guid id = Guid.CreateVersion7();
        // using var session = _store.LightweightSession();
        // session.Store(new DbModels.Offer 
        //     { 
        //         Id = id, 
        //         UserId = req.UserId,
        //         CreatedByUser = req.CreateBy,
        //         SpotId = req.SpotId,
        //         ValidTill = req.ValidTill,
        //         Value = req.Value 
        //     });
        // await session.SaveChangesAsync(ct);

        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(id), ct);
    }
}