using System.Threading.Channels;
using FastEndpoints;
using Marten;
using Marten.Patching;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Helpers;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Features.Shared.Spots;

namespace SpotTool.Web.Features.Offers.Create;

public record CreateOfferRespond(string? ErrorMsg = null, bool IsError = true, Guid? OfferId = null);

public class CreateOfferHandler(
    IDocumentSession _session, 
    SpotDistribution _spotDistributor,
    IDateTimeProvider _dateTimeProvider,
    SpotService _spotService,
    Channel<Guid> _wakeupChannel) : ICommandHandler<CreateOfferCommand, CreateOfferRespond>
{
    public async Task<CreateOfferRespond> ExecuteAsync(CreateOfferCommand req, CancellationToken ct)
    {
        var spot = await _session.Query<DbModels.Spot>().FirstOrDefaultAsync(m=>m.Id == req.SpotId, ct);
        if(spot is null)
            return new CreateOfferRespond("Spot not found");
    
        var createdForUser = await _session.Query<DbModels.User>().FirstOrDefaultAsync(m=>m.Id == req.CreateForUserId, ct);
        if(createdForUser is null)
            return new CreateOfferRespond("User not found");    

        var prevSpotStatus = spot.CurrentStatus;
        var offerStatus = Status.Offer.Ok;
        var createdByUser = createdForUser;
        
        if(!req.CreateByUserId.Equals(req.CreateForUserId))
        {
            offerStatus = Status.Offer.ToNegotiation;
            spot.CurrentStatus = Status.Spot.Negotiation;
            //spot.NegotiationDeadLine = req.ValidTill;

            createdByUser = await _session.Query<DbModels.User>().FirstOrDefaultAsync(m=>m.Id == req.CreateByUserId, ct);
            if(createdByUser is null)
                return new CreateOfferRespond("User by user not found");
        }
        else if(spot.CurrentStatus == Status.Spot.Negotiation)
        {
            offerStatus = Status.Offer.Negotiated;   
        }
        
        spot.CurrentStatus = NextSpotStatus(spot); 

        if(!CanAcceptOffer(spot.CurrentStatus, spot.DeadLine, _dateTimeProvider.Now()))
            return new CreateOfferRespond("Offer cannot be accepted");

        var spotOffers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status == Status.Offer.Ok)
            .OrderBy(m=>m.Value)
            .ToListAsync(ct);
            
        decimal minOfferValue = req.Value;
        DbModels.Offer? minOffer = spotOffers.MinBy(m=>m.Value);

        if(minOffer is not null)
            minOfferValue = minOffer.Value < req.Value ? minOffer.Value : req.Value;
        
        if(spot.CurrentStatus == Status.Spot.Bidding && prevSpotStatus == Status.Spot.Bidding)
        {
            var offersToUpdate = FindOffersToUpdate(minOfferValue, [.. spotOffers.Where(m=>m.Value > req.Value)]);
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
            ValueStatus = SetOfferValueStatus(req.Value, minOfferValue),
            DeltaToMinOffer = CalculateDifferent(req.Value, minOfferValue, true)
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
        
        spotOffers = await _session.Query<DbModels.Offer>()
            .Where(m=>m.SpotId == spot.Id && m.Status != Status.Offer.Deleted)
            .OrderBy(m=>m.Value)
            .ToListAsync(ct);
            
        var spotModel = new SpotModel(
            spot, 
            [.. spotOffers],
            await _spotService.GetSpotGroups(spot.Id, ct),
            await _spotService.GetUsersExcluded(spot.Id, ct) 
        );
        _ = _spotDistributor.SendOfferCreatedToAuthorizedUsers(spotModel, offer);

        if(offer.Status == Status.Offer.ToNegotiation)
            _wakeupChannel.Writer.TryWrite(spot.Id);   

        return new CreateOfferRespond(IsError: false, OfferId: id);     
    }

    private static bool CanAcceptOffer(Status.Spot status, DateTimeOffset spotDeadline, DateTimeOffset currentDatetime)
    {
        if(status == Status.Spot.Ok || status == Status.Spot.Bidding && spotDeadline >= currentDatetime)
            return true;
        else if(status == Status.Spot.Negotiation)
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
        return spot.CurrentStatus;
    }

    // Jeżeli mamy pierwsza oferte zwracamy Green
    // Jeżeli mamy wiecej ofert to
    //1. 
    //1. Pobieramy najnizsza i dla pozostałych wyliczamy % roznicy 
    //2. Jezeli roznica jest mniejsza niz 10% 
    //
    private static decimal CalculateDifferent(decimal addedValue, decimal existedMinOfferValue, bool calculateInPercentage = true)
    {
        return calculateInPercentage ? 100 - (addedValue * 100 / existedMinOfferValue) : existedMinOfferValue - addedValue;
    }
    private static Status.OfferValue SetOfferValueStatus(decimal value, decimal minOfferValue)
    {
        decimal different = CalculateDifferent(value, minOfferValue);
        return CalculateOfferStatus(new CalculationModel(different));
    }

    private static Dictionary<Guid, (Status.OfferValue newStatus, decimal difference)> FindOffersToUpdate(decimal minOffer, List<DbModels.Offer> restOfOffers)
    {
        Dictionary<Guid, (Status.OfferValue, decimal difference)> offersToUpdate = [];
        foreach (var item in restOfOffers)
        {
            decimal difference = CalculateDifferent(item.Value, minOffer);
            var newStatus = CalculateOfferStatus(new CalculationModel(difference));

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
    private record CalculationModel(decimal Difference, bool CalculateInPercentage = true);
    private static Status.OfferValue CalculateOfferStatus(CalculationModel model) => model switch
    {
        { Difference: <= -10, CalculateInPercentage: true } => Status.OfferValue.Red,
        { Difference: <= -50, CalculateInPercentage: false } => Status.OfferValue.Red,
        { Difference: >= -9 and <= -6, CalculateInPercentage: true } => Status.OfferValue.Yellow,
        { Difference: >= -49 and <= -25, CalculateInPercentage: false } => Status.OfferValue.Yellow,
        _ => Status.OfferValue.Green  
    };

    // private static Status.OfferValue CalculateOfferStatus(decimal difference) => difference switch
    // {
    //     <= -10 => Status.OfferValue.Red,
    //     >= -9 and <= -6 => Status.OfferValue.Yellow,
    //     _ => Status.OfferValue.Green  
    // }; 

}
