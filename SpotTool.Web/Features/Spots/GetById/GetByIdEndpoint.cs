using FastEndpoints;
using Marten;
using SpotTool.Web.Feature.Shared;

namespace SpotTool.Web.Feature.Spots.GetById;

// --- 2. NOWY ENDPOINT GET (POTRZEBNY JAKO CEL DLA LOCATION) ---
// Służy maszynom/TMS-om do sprawdzenia, czy Spot na pewno istnieje w bazie
public class GetByIdEndpoint(IDocumentStore store) : EndpointWithoutRequest<Spot>
{
    private readonly IDocumentStore _store = store;

    public override void Configure()
    {
        // Trasa przyjmuje {Id}, którego wymaga SendCreatedAtAsync
        Get("/api/v1/integrations/spots/{Id}"); 
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Wyciągamy ID bezpośrednio z adresu URL
        var id = Route<Guid>("Id");
        
        using var session = _store.QuerySession();
        var spot = await session.LoadAsync<Spot>(id, ct);

        if (spot is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(spot, ct);
    }
}