using FastEndpoints;
using SpotTool.Web.Features.Users;

namespace SpotTool.Web.Features.Monitoring;

public record AddMonitoringItemRespond(bool IsError, string? ErrorMesage = null);
public record AddMonitoringItemCommand(string SpotNo, string Issuer, string Bidder, int Count) : ICommand<AddMonitoringItemRespond>;

public class AddMonitoringItemHandler(
    IMonitoringHelper _monitoringHelper) : ICommandHandler<AddMonitoringItemCommand, AddMonitoringItemRespond>
{
    public async Task<AddMonitoringItemRespond> ExecuteAsync(AddMonitoringItemCommand command, CancellationToken ct)
    {
        _monitoringHelper.AddItems(command.Issuer, command.Bidder, command.SpotNo, command.Count);
        return new AddMonitoringItemRespond(false);
    }
}