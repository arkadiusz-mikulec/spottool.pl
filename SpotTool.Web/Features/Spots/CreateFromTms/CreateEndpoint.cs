using System.Threading.Channels;
using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;


namespace SpotTool.Web.Features.Spots.CreateFromTms;

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(string UserEmail, string Route, decimal Price, DateTimeOffset? Deadline, Status.Spot? Status, DbModels.ContactPersonDetail? ContactPerson);
public record Response(Guid SpotId);

// 2. Automatyczny walidator (FastEndpoints odpala go sam!)
public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Route).NotEmpty().WithMessage("Trasa jest wymagana!");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Wartość frachtu musi być dodatnia!");
        RuleFor(x => x.Status)
            .Must(status => status is null or Status.Spot.NotConfirmed or Status.Spot.Ok)
            .WithMessage("Status musi być pusty, albo wynosić NotConfirmed lub Ok.");
        
        // alternatywa. Uruchomi sie tylko jeżeli wartośc statusu nie jest null
        // RuleFor(x => x.Status)
        //     .Must(status => status is Status.Spot.NotConfirmed or Status.Spot.Ok)
        //     .When(x => x.Status is not null) // Reguła działa tylko gdy status ma wartość
        //     .WithMessage("Status musi być pusty, albo wynosić NotConfirmed lub Ok.");

    }
}

// 3. Sam Endpoint + Handler w jednym miejscu
public class CreateEndpoint(
    IDocumentSession _session, 
    SpotChannelManager _spotChannelManager,
    Channel<Guid> _wakeupChannel,
    IDateTimeProvider _dateTimeProvider) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    
    public override void Configure()
    {
        Post("/api/v1/integrations/spots");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        Guid id = Guid.CreateVersion7();
        var user = await _session.Query<DbModels.User>().Where(m=>m.ContactDetails.Email.Equals(req.UserEmail, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefaultAsync(ct);
        if(user is null)
            await Send.NotFoundAsync(ct);
        
        // Console.WriteLine($"User status: {user!.Status}, role: {user.Role}");
        // var contactDetails = req.ContactPerson ?? new DbModels.ContactPersonDetail(user!.ContactDetails.Name, user.ContactDetails.Mobile, user.ContactDetails.Email);;
        var spot = new DbModels.Spot 
        { 
            Id = id, 
            UserId = user!.Id, 
            Description = req.Route, 
            TargetedCost = req.Price,
            ContactDetails = req.ContactPerson,
            CreatedByUser = new DbModels.UserSnapShot(user.Id, user.Role, user.Status, user.ContactDetails),
            CurrentStatus = req.Status ?? Status.Spot.NotConfirmed,
            DeadLine = req.Deadline is null ? 
                _dateTimeProvider.Now().AddMinutes(15) : 
                _dateTimeProvider.FormatDateTimeWithoutSecunds((DateTimeOffset)req.Deadline)
        };

        _session.Store(spot);
        _session.Store(new DbModels.SpotStatusHistory
        {
           SpotId = id,
           SpotStatus = req.Status ?? Status.Spot.NotConfirmed
        });

        await _session.SaveChangesAsync(ct);

        
        await _spotChannelManager.DistributeSpotAsync(spot, ct);
        if(spot.CurrentStatus == Status.Spot.Ok)
            await _wakeupChannel.Writer.WriteAsync(id, ct);
        
        // Błyskawiczna odpowiedź 201 Created
        await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
    }
}