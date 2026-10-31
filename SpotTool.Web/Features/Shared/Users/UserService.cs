using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Cookies;

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


    public async Task<UserProvider?> SetUserProviderData(UserEmailAndTimeOffset data, CancellationToken ct)
    {
        var user = await GetUserByEmailAsync(data.Email, ct);

        if(user is null)
            return null;

        return new UserProvider(user.Id, user.ContactDetails.Email, [], data.Offset, user.Roles, user.Status);
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
public record UserProvider(Guid Id, string Email, List<Guid> UserGroups, TimeSpan UserOffset, List<Role.User> Roles, Status.User Status);
    
