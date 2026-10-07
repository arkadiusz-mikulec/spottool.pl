using Marten;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Users;

public class UserService(IDocumentSession _session)
{
    public async Task<DbModels.User?> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        var user = await _session.Query<DbModels.User>()
            .FirstOrDefaultAsync(m=>m.ContactDetails.Email.Equals(email, StringComparison.CurrentCultureIgnoreCase), ct);
        
        if(user is null || user.Status == Domain.Types.Status.User.Deleted)
            return null;
        return user;
    }
}