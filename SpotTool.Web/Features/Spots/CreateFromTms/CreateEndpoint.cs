using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Db;


namespace SpotTool.Web.Features.Spots.CreateFromTms;

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(Guid UserId, string Route, decimal Price);
public record Response(Guid SpotId);

// 2. Automatyczny walidator (FastEndpoints odpala go sam!)
public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Route).NotEmpty().WithMessage("Trasa jest wymagana!");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Wartość frachtu musi być dodatnia!");
    }
}

// 3. Sam Endpoint + Handler w jednym miejscu
public class CreateEndpoint(IDocumentStore store) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI

    public override void Configure()
    {
        Post("/api/v1/integrations/spots");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
         Guid id = Guid.CreateVersion7();
        // using var session = _store.LightweightSession();
        // session.Store(new DbModels.Spot { Id = id, UserId = req.UserId, Description = req.Route, TargetedCost = req.Price });
        // await session.SaveChangesAsync(ct);

        // // Błyskawiczna odpowiedź 201 Created
         await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
    }
}