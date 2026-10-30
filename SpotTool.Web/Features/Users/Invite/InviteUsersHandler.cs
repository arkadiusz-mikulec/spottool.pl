
using FastEndpoints;
using SpotTool.Web.Features.Users.Create;

namespace SpotTool.Web.Features.Users.Invite;

public record InviteUsersRespond(string? ErrorMsg = null, int StatusCode = StatusCodes.Status500InternalServerError, bool IsError = true);
public record InviteUsersCommand(string[] Emails) : ICommand<InviteUsersRespond>;
public class InviteUsersHandler() : ICommandHandler<InviteUsersCommand, InviteUsersRespond>
{
    public async Task<InviteUsersRespond> ExecuteAsync(InviteUsersCommand command, CancellationToken ct)
    {
        foreach (var email in command.Emails)
        {   
            await new CreateUserCommand(email).ExecuteAsync(ct);
        }
        return new InviteUsersRespond(IsError: false, StatusCode: StatusCodes.Status200OK);
    }

}