using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;


namespace SpotTool.Web.Features.Shared.Spots;

public class SpotService(IDocumentSession session, 
    SpotChannelManager spotChannelManagement)
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

    public async Task<DateTimeOffset?> GetNextSpotDeadlineAsync(CancellationToken ct)
    {
        var result = await session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.DuringBidding || 
                m.CurrentStatus == Status.Spot.DuringNegotiation)
            .OrderBy(m=>m.DeadLine)
            .FirstOrDefaultAsync(ct);
        
        return result?.DeadLine;
    }

    public async Task<Tuple<DateTimeOffset, Guid>?> GetNextSpotDeadlineAndIdAsync(CancellationToken ct)
    {
        var result = await session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.DuringBidding || 
                m.CurrentStatus == Status.Spot.DuringNegotiation)
            .OrderBy(m=>m.DeadLine)
            .FirstOrDefaultAsync(ct);
        
        return result is null ? null : new Tuple<DateTimeOffset, Guid>(result.DeadLine, result.Id);
    }

    // public async Task UpdateSpotStatusAsync(Guid spotId, CancellationToken ct)
    // {
    //     Status.Spot spotStatus = Status.Spot.EndedNoOffers;

    //     var minOrder = await session.Query<DbModels.Offer>()
    //             .Where(m=>m.SpotId == spotId)
    //             .OrderBy(m=>m.Value)
    //             .FirstOrDefaultAsync(ct);

    //     if(minOrder is not null)
    //         spotStatus = item.TargetedCost >= minOrder.Value ? Status.Spot.EndedCostOK : Status.Spot.EndedCostTooHigh; 
        
    //     item.CurrentStatus = spotStatus;

    //     session.Store(item);

    //     session.Patch<DbModels.Spot>(spotId)
    //         .Set(m=>m.CurrentStatus, Status.Spot.EndedCostOK);

    //     await session.SaveChangesAsync(ct);
    //     await updateSpotChannel.Writer.WriteAsync(
    //         new SpotStatusUpdateRequest(spotId, Status.Spot.EndedCostOK), ct);
    // }

    public async Task<List<Guid>> UpdateManySpotsStatusAsync(CancellationToken ct)
    {
        var spotsToUpdate = await session.Query<DbModels.Spot>()
            .Where(m=>m.CurrentStatus == Status.Spot.Ok || 
                m.CurrentStatus == Status.Spot.DuringBidding || 
                m.CurrentStatus == Status.Spot.DuringNegotiation &&
                m.DeadLine <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var item in spotsToUpdate)
        {
            Status.Spot spotStatus = Status.Spot.EndedNoOffers;
            var minOrder = await session.Query<DbModels.Offer>()
                .Where(m=>m.SpotId == item.Id)
                .OrderBy(m=>m.Value)
                .FirstOrDefaultAsync(ct);

            if(minOrder is not null)
                spotStatus = item.TargetedCost >= minOrder.Value ? Status.Spot.EndedCostOK : Status.Spot.EndedCostTooHigh; 
            
            item.CurrentStatus = spotStatus;

            session.Store(item);

            //Moge stworzyc channel gdzie wysle cala liste!
            await spotChannelManagement.DistributeSpotStatusAsync(item, spotStatus, [], ct);    
        }
        await session.SaveChangesAsync(ct);

        return [.. spotsToUpdate.Select(m=>m.Id)];
    } 
}