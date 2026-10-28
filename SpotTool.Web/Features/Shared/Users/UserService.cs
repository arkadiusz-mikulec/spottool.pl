using Marten;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Users;

public class UserService(IQuerySession _session)
{
    public async Task<DbModels.User?> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        var user = await _session.Query<DbModels.User>()
            .FirstOrDefaultAsync(m=>m.ContactDetails.Email.Equals(email, StringComparison.CurrentCultureIgnoreCase), ct);
        
        if(user is null || user.Status == Domain.Types.Status.User.Deleted)
            return null;
        return user;
    }

    //Zastanwic sie nad tym gdzie i jak to implementowac
    //Chwilowo tutaj zeby jedna funkcja nie byla rozproszona po całym projekcie
    //Docelowo funkcja bedzie duzo większa jak wprowadzimy BU
    public bool IsSpotCreator(Guid userId, Guid spotUserId)
    {
        return spotUserId.Equals(userId);
    }
    //Zastanwic sie nad tym gdzie i jak to implementowac
    //Tylko jedna referencja !!!! mogłoby byc lokalnie w tamtym miejscu
    public bool CarrierHasAccess(Guid userId, List<Guid> groupsAssignedToSpot, List<Guid> carrierGroups, List<Guid> usersExcluded)
    {
        bool isGlobal = groupsAssignedToSpot.Count == 0; 
        return isGlobal || carrierGroups.Intersect(groupsAssignedToSpot).Any() || !usersExcluded.Contains(userId);    
    }
}