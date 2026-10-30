using System.Threading.Channels;
using FastEndpoints;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Spots.Create;

public record CreateSpotRespond(string? ErrorMsg = null, bool IsError = true, Guid? SpotId = null);
public record CreateSpotCommand(Guid UserId, string Route, decimal Price, DateTimeOffset? Deadline, Status.Spot? Status, DbModels.ContactPersonDetail? ContactPerson): ICommand<CreateSpotRespond>;

public class CreateSpotHandler(
    IDocumentSession _session,
    IDateTimeProvider _dateTimeProvider,
    SpotService _spotService,
    SpotDistribution _spotDistribution,
    Channel<Guid> _wakeupChannel) : ICommandHandler<CreateSpotCommand, CreateSpotRespond>
{
    public async Task<CreateSpotRespond> ExecuteAsync(CreateSpotCommand req, CancellationToken ct)
    {
        Guid id = Guid.CreateVersion7();
        var user = await _session.LoadAsync<DbModels.User>(req.UserId, ct);
        if(user is null)
            return new CreateSpotRespond("User not found");
        
        // Console.WriteLine($"User status: {user!.Status}, role: {user.Role}");
        // var contactDetails = req.ContactPerson ?? new DbModels.ContactPersonDetail(user!.ContactDetails.Name, user.ContactDetails.Mobile, user.ContactDetails.Email);;
        var spot = new DbModels.Spot 
        { 
            Id = id, 
            UserId = user!.Id, 
            Description = req.Route, 
            TargetedCost = req.Price,
            ContactDetails = req.ContactPerson,
            CreatedByUser = new DbModels.UserSnapShot(user.Id, user.Roles, user.Status, user.ContactDetails),
            CurrentStatus = req.Status ?? Status.Spot.NotConfirmed,
            DeadLine = req.Deadline is null ? 
                _dateTimeProvider.Now().AddMinutes(15) : 
                _dateTimeProvider.FormatDateTimeWithoutSecunds((DateTimeOffset)req.Deadline)
        };

        _session.Store(spot);
        //To powinno byc w modelu ale bedzie to samo musze stworzyc obiekt i wywolac Add()!!!
        _session.Store(new DbModels.SpotStatusHistory
        {
           SpotId = id,
           SpotStatus = req.Status ?? Status.Spot.NotConfirmed
        });

        await _session.SaveChangesAsync(ct);
        
        var spotModel = new SpotModel(
            spot, 
            [],
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct)
        );
        _ = _spotDistribution.SendSpotCreatedToAuthorizedUsers(spotModel);

        if(spot.CurrentStatus == Status.Spot.Ok)
            _wakeupChannel.Writer.TryWrite(id);
        
        return new CreateSpotRespond(IsError: false, SpotId: id);
    }
}