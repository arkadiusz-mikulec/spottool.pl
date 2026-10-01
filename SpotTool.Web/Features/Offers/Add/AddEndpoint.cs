using System.Net;
using System.Reflection.Metadata;
using FastEndpoints;
using FluentValidation;
using Marten;
using Marten.Patching;
using SpotTool.Web.Components;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;



namespace SpotTool.Web.Features.Offers.Add;

// public class Offer
//     {
//         public Guid Id { get; set; } = Guid.CreateVersion7();
//         public Guid SpotId { get; set; }
//         public Guid UserId {get; set;} // FK for CreatedForUser 
//         public Status.Offer Status { get; set; } = Types.Status.Offer.Ok;
//         public decimal Value { get; set; }
//         public Currency.Code CurrencyCode { get; set; } = Currency.Code.EUR;
//         public bool IsWinnerOffer { get; set; } = false;
//         public string? Remarks {get; set;}
//         public DateTimeOffset ValidTill { get; set; } = DateTimeOffset.UtcNow.AddHours(2).ToLocalTime();
//         public DateTimeOffset CreatedAt {get; set;} = DateTimeOffset.UtcNow.ToLocalTime();
//         public required UserSnapShot CreatedByUser { get; set; }
//         public required UserSnapShot CreatedForUser { get; set; }
//         //public OfferFeedbackSnapShot? OfferFeedbackSnapShot { get; set; } - to bedzie czesc kontaktu zwracanego do UI/API
//     }
// 1. Definicja żądania i odpowiedzi (krótkie rekordy)
public record Request(Guid SpotId, Guid CreateByUserId, Guid CreateForUserId, decimal Value, Currency.Code? CurrencyCode, string? Remark, DateTimeOffset? ValidTill);
public record Response(Guid OfferId);

// 2. Automatyczny walidator (FastEndpoints odpala go sam!)
public class Validator : Validator<Request>
{
    public Validator()
    {
        //RuleFor(x => x.ValidTill).GreaterThan(DateTimeOffset.Now).WithMessage("Oferta musi być ważna dłużej niż NOW!");
        RuleFor(x => x.Value).GreaterThan(0).WithMessage("Wartość oferty musi być dodatnia!");
    }
}

// 3. Sam Endpoint + Handler w jednym miejscu
public class AddEndpoint(IDocumentSession session) : Endpoint<Request, Response> //EndpointWithoutRequest<Response>
{
    //private readonly IDocumentStore _store = store; // Marten wstrzyknięty klasycznie przez DI
    
    public override void Configure()
    {
        Post("/api/v1/integrations/offers");
        AllowAnonymous(); // Lub SetupSecurity/Policies dla API Key
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Logika biznesowa zapisu do Martena/Postgresa
        
        var spot = await session.Query<DbModels.Spot>().FirstOrDefaultAsync(m=>m.Id == req.SpotId, ct);
        if(spot is null)
        {
            await Send.NotFoundAsync(ct);
            return; //Task.FromResult(new ProblemDetails();
        }
        
        if(!CanAcceptOffer(spot!.CurrentStatus, spot.DeadLine))
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: "Offer cannot be accepted.",
                statusCode: (int)HttpStatusCode.BadRequest,
                title: "Bad request - add offer"
            ));
            // await Send.ResultAsync(Results.Problem(
            //     detail: "",
            //     statusCode: (int)HttpStatusCode.BadRequest,
            //     title: ""
            // ));
            return;
        }

        var createdForUser = await session.Query<DbModels.User>().FirstOrDefaultAsync(m=>m.Id == req.CreateForUserId, ct);
        if(createdForUser is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var offerStatus = Status.Offer.Ok;
        var createdByUser = createdForUser;

        if(!req.CreateByUserId.Equals(req.CreateForUserId))
        {
            offerStatus = Status.Offer.ToNegotiation;
            createdByUser = await session.Query<DbModels.User>().FirstOrDefaultAsync(m=>m.Id == req.CreateByUserId, ct);
            if(createdByUser is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
    
        } 

        var spotOffers = await session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status == Status.Offer.Ok)
            .OrderBy(m=>m.Value)
            .ToAsyncEnumerable(ct)
            .ToListAsync(ct);
            // .Select(m=> ValueTuple.Create(m.Id, m.Value, m.ValueStatus))
            // .OrderBy(m=>m.Item2)
            // .ToAsyncEnumerable(ct)
            // .ToListAsync(ct);
        // if(spotOffers.Count > 0)
        // {
            
        // }
        var minOffer = spotOffers.Count > 0 ? spotOffers[0] : null;
        decimal minOfferValue = minOffer is null ? 
            req.Value : 
            minOffer!.Value < req.Value ? 
                minOffer.Value : 
                req.Value
        ;

        Guid id = Guid.CreateVersion7();
        var offer = new DbModels.Offer
        { 
            Id = id, 
            UserId = req.CreateForUserId,
            CreatedByUser = new DbModels.UserSnapShot(createdByUser!.Id, createdByUser.Role, createdByUser.Status, createdByUser.ContactDetails),
            CreatedForUser = new DbModels.UserSnapShot(createdForUser!.Id, createdForUser.Role, createdForUser.Status, createdForUser.ContactDetails),
            SpotId = req.SpotId,
            ValidTill = req.ValidTill ?? DateTimeOffset.UtcNow.AddHours(2),
            Value = req.Value,
            CurrencyCode = req.CurrencyCode ?? Currency.Code.EUR,
            Remarks = req.Remark,
            Status = offerStatus,
            ValueStatus = SetOfferValueStatus(req.Value, minOffer).Item1
        };
        session.Store(offer);

        var offersToUpdate = FindOffersToUpdate(minOfferValue, [.. spotOffers.Where(m=>m.Value > minOfferValue)]);
        UpdateOffers(session, offersToUpdate);

        var nextStatus = NextSpotStatus(spot, req.Value);
        if(spot.CurrentStatus != nextStatus)
        {
            spot.CurrentStatus = nextStatus;
            
            session.Update(spot);
            //session.Patch<DbModels.Spot>(spot.Id).Set(m=>m.CurrentStatus, nextStatus);
            session.Store(new DbModels.SpotStatusHistory
            {
                SpotId = req.SpotId,
                SpotStatus = nextStatus
            });                    
        }

        await session.SaveChangesAsync(ct);

        // // Błyskawiczna odpowiedź 201 Created
        // //await Send.CreatedAtAsync<GetById.GetByIdEndpoint>(new { Id = id }, new Response(id), cancellation: ct);
        await Send.OkAsync(new Response(id), ct);
    }

    private static bool CanAcceptOffer(Status.Spot status, DateTimeOffset spotDeadline)
    {
        if(status == Status.Spot.Ok || status == Status.Spot.DuringBidding && spotDeadline.Ticks >= DateTimeOffset.UtcNow.Ticks)
            return true;
        else if(status == Status.Spot.DuringNegotiation)
            return true;

        return false;
    }

    private static Status.Spot NextSpotStatus(DbModels.Spot spot, decimal value)
    {
        //bool afterDeadLine = spot.DeadLine >= DateTimeOffset.UtcNow;

        if(spot.CurrentStatus == Status.Spot.DuringNegotiation)
        {
            return spot.TargetedCost < value ? Status.Spot.EndedCostTooHigh : Status.Spot.EndedCostOK;
        }
        else if(spot.CurrentStatus == Status.Spot.Ok)
            return Status.Spot.DuringBidding;

        return spot.CurrentStatus;
    }

    // Jeżeli mamy pierwsza oferte zwracamy Green
    // Jeżeli mamy wiecej ofert to
    //1. 
    //1. Pobieramy najnizsza i dla pozostałych wyliczamy % roznicy 
    //2. Jezeli roznica jest mniejsza niz 10% 
    //
    private static (Status.OfferValue, decimal) SetOfferValueStatus(decimal value, DbModels.Offer? minOffer)
    {
        //int maxCountForOneGreenOffer = 3;
        (Status.OfferValue offerStatus, decimal difference) result = (Status.OfferValue.Green, 0);
    
        if(minOffer is null)
            return result;

        decimal different = minOffer.Value - value;
        result = (CalculateOfferStatus(different), different);

        return result;
    }

    private static Dictionary<Guid, (Status.OfferValue newStatus, decimal difference)> FindOffersToUpdate(decimal minOffer, List<DbModels.Offer> restOfOffers)
    {
        Dictionary<Guid, (Status.OfferValue, decimal difference)> offersToUpdate = [];
        foreach (var item in restOfOffers)
        {
            decimal difference = minOffer - item.Value;
            
            var newStatus = CalculateOfferStatus(difference);

            if(newStatus != item.ValueStatus)
                offersToUpdate[item.Id] = (newStatus, difference);
        }
        return offersToUpdate;
    }

    private static void UpdateOffers(IDocumentSession session, Dictionary<Guid, (Status.OfferValue newStatus, decimal difference)> offersToUpdate)
    {
        foreach (var item in offersToUpdate.Keys)
        {
            session.Patch<DbModels.Offer>(item)
                .Set(m=>m.ValueStatus, offersToUpdate[item].newStatus);
        }
    }
    private static Status.OfferValue CalculateOfferStatus(decimal difference) => difference switch
    {
        <= -25 => Status.OfferValue.Red,
        >= -24 and <= -11 => Status.OfferValue.Yellow,
        _ => Status.OfferValue.Green  
    }; 
}