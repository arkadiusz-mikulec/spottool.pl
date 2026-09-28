using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using SpotTool.Web.Domain;
using SpotTool.Web.Infrastructure;

namespace SpotTool.Web.Features.Shared.Spots;

public class SpotServiceInMemory: ISpotService
{
    private readonly ConcurrentDictionary<Guid, DbModels.Spot> db = new();
    public SpotServiceInMemory()
    {
        db[InitModels.spotId] = InitModels.Spots()[0];
    }
    public async Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct)
    {
        if(!db.TryGetValue(id, out DbModels.Spot? spot))
            return null;
        return spot;
    }

    public async Task<IReadOnlyList<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    {
        List<DbModels.Spot> result = [];
        foreach (var entry in db)
        {
            if(entry.Value is not null)
                result.Add(entry.Value);
        }
        return result;
    }
}
