using System.Threading.Channels;
using FastEndpoints;
using FluentValidation;
using Marten;
using Marten.Patching;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;


namespace SpotTool.Web.Features.Offers.Create;

//CreateOfferCommand wykorzystawany w Handlerze.
//Gdyby były potrzeba dodatkowe pola to w API mozna je dodac wowczas rodzielamy Command od Reqest
//Ale tak długo jak Command i Request maja takie same pola mozna wykorzystac obiekt w obu miejscach

//Http respond
public record Response(Guid OfferId);


// 2. Automatyczny walidator (FastEndpoints odpala go sam!)
public class Validator : Validator<CreateOfferCommand>
{
    public Validator()
    {
        //RuleFor(x => x.ValidTill).GreaterThan(_dateTimeProvider.Now()ithMessage("Oferta musi być ważna dłużej niż NOW!");
        RuleFor(x => x.Value).GreaterThan(0).WithMessage("Wartość oferty musi być dodatnia!");
    }
}

// 3. Sam Endpoint + Handler w jednym miejscu
public class CreateEndpoint : Endpoint<CreateOfferCommand, Response> //EndpointWithoutRequest<Response>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Post("/api/v1/integrations/offers");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(CreateOfferCommand req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        
        var createOfferCommand = await req.ExecuteAsync(ct);

        if(createOfferCommand.IsError)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: createOfferCommand.ErrorMsg,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Offer cannot be created"
            ));
            
            return;
        }

        await Send.OkAsync(new Response((Guid)createOfferCommand.OfferId!), ct);
    }
}