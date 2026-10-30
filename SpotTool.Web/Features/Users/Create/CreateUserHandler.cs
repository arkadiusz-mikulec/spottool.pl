using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Features.Users.Create;

public record CreateUserRespond(string? ErrorMsg = null, int StatusCode = StatusCodes.Status404NotFound, bool IsError = true);
public record CreateUserCommand(string Email) : ICommand<CreateUserRespond>;
public class CreateUserHandler(
    UserService _userService,
    IDocumentSession _session) : ICommandHandler<CreateUserCommand, CreateUserRespond>
{
    public async Task<CreateUserRespond> ExecuteAsync(CreateUserCommand command, CancellationToken ct)
    {
        var user = await _userService.GetUserByEmailAsync(command.Email, ct);
        if(user is null)
        {
            user = new DbModels.User
            {
                ContactDetails = new DbModels.ContactPersonDetail(command.Email, "+48", command.Email.ToLower()),
                Status = Status.User.Ok
            };

            _session.Store(user);
            await _session.SaveChangesAsync(ct);            
        }
        return new CreateUserRespond(IsError: false, StatusCode: StatusCodes.Status200OK);
    }

}
