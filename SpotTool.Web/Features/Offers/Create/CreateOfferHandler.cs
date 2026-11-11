using System.Threading.Channels;
using FastEndpoints;
using Marten;
using Marten.Patching;
using SpotTool.Web.Components;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Offers;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Offers.Create;

public record CreateOfferRespond(string? ErrorMsg = null, bool IsError = true, Guid? OfferId = null);

public record CreateOfferCommand(Guid SpotId, Guid CreateByUserId, Guid CreateForUserId, decimal Value, 
    Currency.Code? CurrencyCode, string? Remark, DateTimeOffset? ValidTill) : ICommand<CreateOfferRespond>;


public class CreateOfferHandler(
    IDocumentSession _session, 
    SpotDistribution _spotDistributor,
    IDateTimeProvider _dateTimeProvider,
    SpotService _spotService,
    Channel<Guid> _wakeupChannel,
    OfferService _offerService,
    IConfiguration _appConfig) : ICommandHandler<CreateOfferCommand, CreateOfferRespond>
{
    public async Task<CreateOfferRespond> ExecuteAsync(CreateOfferCommand req, CancellationToken ct)
    {
        var spot = await _session.LoadAsync<DbModels.Spot>(req.SpotId, ct);

        if(spot is null)
            return new CreateOfferRespond("Spot not found");
    
        var createdForUser = await _session.LoadAsync<DbModels.User>(req.CreateForUserId, ct);

        if(createdForUser is null)
            return new CreateOfferRespond("User not found");    

        var prevSpotStatus = spot.CurrentStatus;
        var offerStatus = Status.Offer.Ok;
        var createdByUser = createdForUser;
        
        if(!req.CreateByUserId.Equals(req.CreateForUserId))
        {
            offerStatus = Status.Offer.ToNegotiation;
            spot.CurrentStatus = Status.Spot.Negotiation;

            createdByUser = await _session.LoadAsync<DbModels.User>(req.CreateByUserId, ct);
            if(createdByUser is null)
                return new CreateOfferRespond("User by user not found");
        }
        else if(prevSpotStatus == Status.Spot.Negotiation)
        {
            offerStatus = Status.Offer.Negotiated;

            var offersDuringNegotiation = await _session.Query<DbModels.Offer>()
                .Where(m=>m.SpotId == spot.Id && m.Status == Status.Offer.ToNegotiation)
                .ToListAsync(ct);
            
            if(offersDuringNegotiation is null || offersDuringNegotiation.Count == 0)
                return new CreateOfferRespond("offersDuringNegotiation was not found");

            var offerToReject = offersDuringNegotiation.FirstOrDefault(m=>m.UserId == req.CreateForUserId);
            if(offerToReject is null)
                return new CreateOfferRespond("Offer To Reject was not found");

            offerToReject.Status = Status.Offer.Rejected;
            offerToReject.RejectReson = $"Bidder: {createdForUser.ContactDetails.Email} added new offer: {req.Value} instead of accept negotiation offer: {offerToReject.Value}";

            _session.Store(offerToReject);
            //await _session.SaveChangesAsync(ct);

            //Jeżeli była tylko jedna/ostatnia to mozemy zamknac spot
            //bo po zapisaniu nie bedzie juz ofert ze statusem 'ToNegotiation'
            if(offersDuringNegotiation.Count == 1)
                spot.CurrentStatus = Status.Spot.Ended;
        }
        else
            spot.CurrentStatus = NextSpotStatus(spot); 

        if(!CanAcceptOffer(spot.CurrentStatus, prevSpotStatus, spot.DeadLine, _dateTimeProvider.Now()))
            return new CreateOfferRespond($"Offer cannot be accepted preStatus: {prevSpotStatus}");

        OfferStatusCalculationDataModel model = InitModel(_appConfig);

        var minOffer = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status == Status.Offer.Ok)
            .OrderBy(m=>m.Value)
            .FirstOrDefaultAsync(ct);
        

        decimal minOfferValue = req.Value;
        if(minOffer is not null)
            minOfferValue = minOffer.Value < req.Value ? minOffer.Value : req.Value;


        if(spot.CurrentStatus == Status.Spot.Bidding && prevSpotStatus == Status.Spot.Bidding)
        {
            var lastOffers = await _offerService.GetLastOffersForSpotAsync(spot, ct);
            var offersToUpdate = FindOffersToUpdate(minOfferValue, [.. lastOffers.Where(m=>m.Value > req.Value)], model);
            UpdateOffers(_session, offersToUpdate);
        }

        Guid id = Guid.CreateVersion7();
        var offer = new DbModels.Offer
        { 
            Id = id, 
            UserId = req.CreateForUserId,
            CreatedByUser = new DbModels.UserSnapShot(createdByUser!.Id, createdByUser.Roles, createdByUser.Status, createdByUser.ContactDetails),
            CreatedForUser = new DbModels.UserSnapShot(createdForUser!.Id, createdForUser.Roles, createdForUser.Status, createdForUser.ContactDetails),
            SpotId = req.SpotId,
            ValidTill = req.ValidTill is not null ? _dateTimeProvider.FormatDateTimeWithoutSecunds((DateTimeOffset)req.ValidTill) : spot.DeadLine.AddHours(2),
            Value = req.Value,
            CurrencyCode = req.CurrencyCode ?? Currency.Code.EUR,
            Remarks = req.Remark?.Trim().ReplaceLineEndings(" "),
            Status = offerStatus,
            ValueStatus = SetOfferValueStatus(req.Value, minOfferValue, model),
            DeltaToMinOffer = CalculateDifferent(req.Value, minOfferValue, model.CalculateInPercentage)
        };

        if(spot.BiddersId is not null)
        {
            if(!spot.BiddersId.Contains(req.CreateForUserId))
               spot.BiddersId.Add(req.CreateForUserId);   
        }
        else
            spot.BiddersId = [req.CreateForUserId];

        if(spot.CurrentStatus != prevSpotStatus)
        {
            _session.Store(new DbModels.SpotStatusHistory
            {
                SpotId = req.SpotId,
                SpotStatus = spot.CurrentStatus
            });
        }

        _session.Store(spot);
        _session.Store(offer);
        
        await _session.SaveChangesAsync(ct);
        
        var spotModel = new SpotModel(
            spot, 
            await _offerService.GetLastOffersForSpotAsync(spot, ct),
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct) 
        );

        if(offer.Status == Status.Offer.ToNegotiation)
            _wakeupChannel.Writer.TryWrite(spot.Id);  

        if(offerStatus == Status.Offer.Negotiated || offerStatus == Status.Offer.ToNegotiation)
            _ = _spotDistributor.SendNegotiationOfferCreatedToIssuerAndBidder(spotModel, offer, [spot.UserId, offer.CreatedForUser.Id]);
        else
            _ = _spotDistributor.SendOfferCreatedToAuthorizedUsers(spotModel, offer);

        return new CreateOfferRespond(IsError: false, OfferId: offer.Id);     
    }

    
    private static bool CanAcceptOffer(Status.Spot status, Status.Spot prevStatus, DateTimeOffset spotDeadline, DateTimeOffset currentDatetime)
    {
        if(status == Status.Spot.Ok || status == Status.Spot.Bidding && spotDeadline >= currentDatetime)
            return true;
        else if(status == Status.Spot.Negotiation)
            return true;
        else if(status == Status.Spot.Ended && prevStatus == Status.Spot.Negotiation)
            return true;

        return false;
    }

    private static Status.Spot NextSpotStatus(DbModels.Spot spot)
    {
        //bool afterDeadLine = spot.DeadLine >= _dateTimeProvider.Now();

        // if(spot.CurrentStatus == Status.Spot.Negotiation)
        // {
        //     return spot.TargetedCost < value ? Status.Spot.EndedCostTooHigh : Status.Spot.EndedCostOK;
        // }
        // else if(spot.CurrentStatus == Status.Spot.Ok)
        //     return Status.Spot.Bidding;

        if(spot.CurrentStatus == Status.Spot.Ok)
            return Status.Spot.Bidding;
        
        if(spot.CurrentStatus == Status.Spot.Negotiation)
        {
            //TODO
            //Check seetings if many negotiation is allowed
            //if yes return Status.Spot.Negotiation
            //else check if this is the last one
            //if not the last one return Status.Spot.Negotiation
            //else retrun Status.Spot.Ended

            return spot.CurrentStatus;
        }
        return spot.CurrentStatus;
    }

    // Jeżeli mamy pierwsza oferte zwracamy Green
    // Jeżeli mamy wiecej ofert to
    //1. 
    //1. Pobieramy najnizsza i dla pozostałych wyliczamy % roznicy 
    //2. Jezeli roznica jest mniejsza niz 10% 
    //
    private static decimal CalculateDifferent(decimal addedValue, decimal existedMinOfferValue, bool calculateInPercentage)
    {
        return calculateInPercentage ? 100 - (addedValue * 100 / existedMinOfferValue) : existedMinOfferValue - addedValue;
    }
    private static Status.OfferValue SetOfferValueStatus(decimal value, decimal minOfferValue, OfferStatusCalculationDataModel model)
    {
        decimal different = CalculateDifferent(value, minOfferValue, model.CalculateInPercentage);
        return CalculateOfferStatus(different, model);
    }

    private static Dictionary<Guid, (Status.OfferValue newStatus, decimal difference)> FindOffersToUpdate(
        decimal minOffer, List<DbModels.Offer> restOfOffers, OfferStatusCalculationDataModel model)
    {
        Dictionary<Guid, (Status.OfferValue, decimal difference)> offersToUpdate = [];
        foreach (var item in restOfOffers)
        {
            decimal difference = CalculateDifferent(item.Value, minOffer, model.CalculateInPercentage);
            var newStatus = CalculateOfferStatus(difference, model);

            Console.WriteLine($"Dif for offer value: {item.Value} and minValue: {minOffer} is {difference}. New status: {newStatus}");   
            
            if(newStatus != item.ValueStatus)
            {
                offersToUpdate[item.Id] = (newStatus, difference);
                Console.WriteLine($"dodaje do aktualizacja value: {item.Value} new status: {newStatus}");
            }
        }
        return offersToUpdate;
    }

    private static void UpdateOffers(IDocumentSession _session, Dictionary<Guid, (Status.OfferValue newStatus, decimal difference)> offersToUpdate)
    {
        foreach (var item in offersToUpdate.Keys)
        {
            Console.WriteLine($"Aktualizjuje ID: {item} with status: {offersToUpdate[item].newStatus} and Delta: {offersToUpdate[item].difference}");
            _session.Patch<DbModels.Offer>(item)
                .Set(m=>m.ValueStatus, offersToUpdate[item].newStatus)
                .Set(m=>m.DeltaToMinOffer, offersToUpdate[item].difference);
        }
    }
    private static Status.OfferValue CalculateOfferStatus(decimal difference, OfferStatusCalculationDataModel model)
    {
        int greenMax = 0;
        if(model.OneGreenDot)
        {
            if(difference >= greenMax) return Status.OfferValue.Green;
            else if(difference < greenMax && difference >= model.YellowMin) return Status.OfferValue.Yellow;
            else return Status.OfferValue.Red;
        }
        else
        {
            if(difference >= model.GreenMin) return Status.OfferValue.Green;
            else if(difference < model.GreenMin && difference >= model.YellowMin) return Status.OfferValue.Yellow;
            else return Status.OfferValue.Red;
        }
    }

    private static OfferStatusCalculationDataModel InitModel(IConfiguration appConfig)
    {
        bool calculateInPercentage = Convert.ToBoolean(appConfig["OfferStatusSettings:CalculateInPercentage"]);
        bool oneGreenDot = Convert.ToBoolean(appConfig["OfferStatusSettings:OneGreenDot"]);

        if(calculateInPercentage)
        {
            return new (
                Convert.ToInt32(appConfig["OfferStatusSettings:Percentage:Green:Min"] ?? "-5"),
                Convert.ToInt32(appConfig["OfferStatusSettings:Percentage:Yellow:Min"] ?? "-10"),
                oneGreenDot, calculateInPercentage
            );
        }
        else
        {
            return new (
                Convert.ToInt32(appConfig["OfferStatusSettings:Value:Green:Min"] ?? "-25"),
                Convert.ToInt32(appConfig["OfferStatusSettings:Value:Yellow:Min"] ?? "-50"),
                oneGreenDot, calculateInPercentage
            );
        }
    }
    private record OfferStatusCalculationDataModel(int GreenMin, int YellowMin, bool OneGreenDot, bool CalculateInPercentage);

    // private static Status.OfferValue CalculateOfferStatusInPerentage(decimal Difference) => Difference switch
    // {

    //     { Difference: <= -10, CalculateInPercentage: true } => Status.OfferValue.Red,
    //     { Difference: <= -50, CalculateInPercentage: false } => Status.OfferValue.Red,
    //     { Difference: >= -9 and <= -6, CalculateInPercentage: true } => Status.OfferValue.Yellow,
    //     { Difference: >= -49 and <= -25, CalculateInPercentage: false } => Status.OfferValue.Yellow,
    //     _ => Status.OfferValue.Green  
    // };

}
