using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Features.Lead;

public record AddLeadRespond(bool Added);
public record AddLeadCommand(string? Name, string? Email, string? Mobile, string? Operation, string? Platform): ICommand<AddLeadRespond>;

public class AddLeadHandler(IDocumentSession _session) : ICommandHandler<AddLeadCommand, AddLeadRespond>
{
    public async Task<AddLeadRespond> ExecuteAsync(AddLeadCommand command, CancellationToken ct)
    {
        _session.Store(new DbModels.Lead
        {
            Email = command.Email,
            Mobile = command.Mobile,
            Name = command.Name,
            Operation = command.Operation,
            Platform = command.Platform
        });
        
        try
        {
            await _session.SaveChangesAsync(ct);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Exception] at AddLeadCommand. Msg: {ex.Message}");
            return new AddLeadRespond(false);
        }

        return new AddLeadRespond(true);
    }
}