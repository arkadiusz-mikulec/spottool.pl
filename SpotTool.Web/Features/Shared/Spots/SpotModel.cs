using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Shared.Spots;

public record SpotModel(DbModels.Spot Spot, List<DbModels.Offer>? Offers, List<Guid>? GroupsAssignedToSpot, List<Guid>? UsersExcludedFromSpot);