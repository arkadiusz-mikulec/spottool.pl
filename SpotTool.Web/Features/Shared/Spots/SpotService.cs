using System.Threading.Channels;
using Marten;
using Marten.Patching;
using SpotTool.Web.Contracts.Spot;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Shared.Spots;

public class SpotService(IDocumentSession session, 
    Channel<SpotStatusUpdateRequest> updateSpotChannel)
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

    public async Task<List<DbModels.Spot>> GetSpotsAsync(CancellationToken ct)
    {
        //using var session = _store.QuerySession();
        return await session.Query<DbModels.Spot>().ToAsyncEnumerable(ct).ToListAsync(ct);
    }

    public async Task<Tuple<DateTimeOffset, Guid>?> GetNextSpotDeadlineAndIdAsync(CancellationToken ct)
    {
        var result = await session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Domain.Types.Status.Spot.Ok || 
                m.CurrentStatus == Domain.Types.Status.Spot.DuringBidding || 
                m.CurrentStatus == Domain.Types.Status.Spot.DuringNegotiation)
            .OrderBy(m=>m.DeadLine)
            .FirstOrDefaultAsync(ct);
        
        return result is null ? null : new Tuple<DateTimeOffset, Guid>(result.DeadLine, result.Id);
    }

    public async Task UpdateSpotStatusAsync(Guid spotId, CancellationToken ct)
    {
        //var spot = await session.LoadAsync<DbModels.Spot>(spotId);
        session.Patch<DbModels.Spot>(spotId)
            .Set(m=>m.CurrentStatus, Domain.Types.Status.Spot.EndedCostOK);

        await session.SaveChangesAsync();
        await updateSpotChannel.Writer.WriteAsync(
            new SpotStatusUpdateRequest(spotId, Domain.Types.Status.Spot.EndedCostOK), ct);
    } 
}