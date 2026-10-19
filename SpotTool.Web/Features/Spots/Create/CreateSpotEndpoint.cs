using System.Threading.Channels;
using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Contracts.UI.Spot;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Channels;
using SpotTool.Web.Features.Shared.Spots;


namespace SpotTool.Web.Features.Spots.Create;

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(Guid UserId, string Route, decimal Price, DateTimeOffset? Deadline, Status.Spot? Status, DbModels.ContactPersonDetail? ContactPerson);
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
public class CreateSpotEndpoint : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    
    public override void Configure()
    {
        Post("/api/v1/integrations/spots");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        var createCommandResult = await new CreateSpotCommand(req.UserId, req.Route, req.Price, req.Deadline, req.Status, req.ContactPerson).ExecuteAsync(ct);
        if(createCommandResult.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: createCommandResult.ErrorMsg,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Spot cannot be created"
            ));
            
            return;
        }
            
        // Błyskawiczna odpowiedź 201 Created
        await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = createCommandResult.SpotId }, new Response((Guid)createCommandResult.SpotId!), cancellation: ct);
    }
}