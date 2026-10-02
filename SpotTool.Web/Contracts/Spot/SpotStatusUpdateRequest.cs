using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Contracts.Spot;

public record SpotStatusUpdateRequest(Guid SpotId, Status.Spot Status);