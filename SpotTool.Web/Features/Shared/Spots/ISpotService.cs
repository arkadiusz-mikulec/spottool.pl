using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Shared.Spots;

public interface ISpotService
{
    Task<DbModels.Spot?> GetSpotByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DbModels.Spot>> GetSpotsAsync(CancellationToken ct);
}