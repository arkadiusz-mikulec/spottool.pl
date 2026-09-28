using Marten;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Shared.Spots;

public class SpotServiceMarten(IDocumentSession session): ISpotService
{
    //private readonly IDocumentSession _session = session;

    public async Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct)
    {
        //using var session = _store.QuerySession();
        var spot = await session.LoadAsync<DbModels.Spot>(id, ct);

        if (spot is null)
            return null;
        return spot;
    }

    public async Task<IReadOnlyList<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    {
        //using var session = _store.QuerySession();
        return await session.Query<DbModels.Spot>().ToListAsync(ct);
    }
}