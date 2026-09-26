using Marten;
using SpotTool.Web.Db;

namespace SpotTool.Web.Features.Shared.Services;

public class SpotService(IDocumentStore store)
{
    private readonly IDocumentStore _store = store;

    public async Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct)
    {
        using var session = _store.QuerySession();
        var spot = await session.LoadAsync<DbModels.Spot>(id, ct);

        if (spot is null)
            return null;
        return spot;
    }

    public async Task<IReadOnlyList<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    {
        using var session = _store.QuerySession();
        return await session.Query<DbModels.Spot>().ToListAsync(ct);
    }
}