using FastEndpoints;
using FluentValidation;
using Marten;
using SpotTool.Web.Domain;
using SpotTool.Web.Features.Shared.Spots;


namespace SpotTool.Web.Features.Spots.CreateFromTms;

// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(string UserEmail, string Route, decimal Price, DbModels.ContactPersonDetail? ContactPerson);
public record Response(Guid SpotId);

// 2. Automatyczny walidator (FastEndpoints odpala go sam!)
public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Route).NotEmpty().WithMessage("Trasa jest wymagana!");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Wartość frachtu musi być dodatnia!");
    }
}

// 3. Sam Endpoint + Handler w jednym miejscu
public class CreateEndpoint(IDocumentSession session) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
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
        var user = await session.Query<DbModels.User>().Where(m=>m.ContactDetails.Email.Equals(req.UserEmail, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefaultAsync(ct);
        if(user is null)
            await Send.NotFoundAsync(ct);
        
        var contactDetails = req.ContactPerson ?? new DbModels.ContactPersonDetail(user!.ContactDetails.Name, user.ContactDetails.Mobile, user.ContactDetails.Email);;

        session.Store(new DbModels.Spot { 
            Id = id, 
            UserId = user!.Id, 
            Description = req.Route, 
            TargetedCost = req.Price,
            ContactDetails = contactDetails,
            CreatedByUser = new DbModels.UserSnapShot(user.Id, user.Role, user.Status, contactDetails) 
        });
        await session.SaveChangesAsync(ct);

        // Błyskawiczna odpowiedź 201 Created
         await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
    }
}