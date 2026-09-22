using FastEndpoints;
using Marten;
using SpotTool.Web.Features.Shared;
using SpotTool.Web.Features.Shared.Services;



namespace SpotTool.Web.Features.Spots.GetById;

// --- 2. NOWY ENDPOINT GET (POTRZEBNY JAKO CEL DLA LOCATION) ---
// Służy maszynom/TMS-om do sprawdzenia, czy Spot na pewno istnieje w bazie
public class GetByIdEndpoint(SpotService spotService) : EndpointWithoutRequest<DbModels.Spot>
{
    private readonly SpotService _spotService = spotService;
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

        var spot = await _spotService.GetSpotByIdAsync(id, ct);
        
        if (spot is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(spot, ct);
    }
}