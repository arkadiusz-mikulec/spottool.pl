using Npgsql.Replication;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using static SpotTool.Web.Domain.DbModels;

namespace SpotTool.Web.Infrastructure;

public static class InitModels
{
    public static readonly Guid spotId = Guid.CreateVersion7();
    private static readonly Guid winnerOfferId = Guid.CreateVersion7();
    private static readonly DbModels.UserSnapShot dispo = new(
        InitModels.Users()[0].Id, InitModels.Users()[0].Roles, InitModels.Users()[0].Status, 
        InitModels.Users()[0].ContactDetails!);
    private static readonly DbModels.UserSnapShot carrier = new (
        InitModels.Users()[2].Id, InitModels.Users()[2].Roles, InitModels.Users()[2].Status, 
        InitModels.Users()[2].ContactDetails!);
    
    public static DbModels.User[] Users()
    {
        var initialUsers = new[]
        {
            new DbModels.User { 
                ContactDetails = new DbModels.ContactPersonDetail("Arek", "+48881250136", "arkadiusz.mikulec@gmail.com"),  
                Roles = [Role.User.Admin],
                Status = Status.User.Ok
            },
            new DbModels.User { 
                ContactDetails = new DbModels.ContactPersonDetail("Arek", "+48881250000", "amikulec@sostmeier.pl"),  
                Roles = [Role.User.Issuer, Role.User.Bidder],
                Status = Status.User.Ok
            },
            new DbModels.User { 
                ContactDetails = new DbModels.ContactPersonDetail("Biuro", "+48881250111", "biuro@e-site.pl"),  
                Roles = [Role.User.Bidder],
                Status = Status.User.Ok
            },
        };
        return initialUsers;
    }
    
    public static DbModels.Spot[] Spots()
    {
        var initialSpots = new[]
        {
            new DbModels.Spot {
                Id = spotId, 
                Description = "Spot1", TargetedCost = 400, 
                UserId = dispo.Id, 
                ContactDetails = dispo.PersonDetail, 
                CurrentStatus = Status.Spot.Accepted,
                CreatedByUser = dispo,
                WinnerOfferSnapShot = new DbModels.OfferSnapShot(
                    (Guid)winnerOfferId!, 
                    carrier, 
                    450, 
                    DateTimeOffset.Now, 
                    Status.Offer.Negotiated, 
                    "450e min.", 
                    DateTimeOffset.Now.AddHours(2),
                    Currency.Code.EUR
                )
            }
        };
        return initialSpots;
    }

    public static DbModels.SpotStatusHistory[] SpotStatusHistory()
    {
        var initSpotStatusHistory = new []
        {
            new DbModels.SpotStatusHistory
            {
                SpotId = spotId,
                CreatedByUser = dispo,
                SpotStatus = Status.Spot.Ok,
                //CreatedAt = DateTimeOffset.UtcNow.ToLocalTime()
            },
            new DbModels.SpotStatusHistory  
            {
                SpotId = spotId,
                CreatedByUser = carrier,
                SpotStatus = Status.Spot.Bidding,
                //CreatedAt = DateTimeOffset.UtcNow.AddMinutes(2).ToLocalTime()
            },
            new DbModels.SpotStatusHistory
            {
                SpotId = spotId,
                CreatedByUser = carrier,
                SpotStatus = Status.Spot.EndedCostTooHigh,
                //CreatedAt = DateTimeOffset.UtcNow.AddMinutes(17).ToLocalTime()
            },
            new DbModels.SpotStatusHistory
            {
                SpotId = spotId,
                CreatedByUser = dispo,
                SpotStatus = Status.Spot.Negotiation,
                //CreatedAt = DateTimeOffset.UtcNow.AddMinutes(15).ToLocalTime()
            },
            new DbModels.SpotStatusHistory
            {
                SpotId = spotId,
                CreatedByUser = carrier,
                SpotStatus = Status.Spot.EndedCostTooHigh,
                //CreatedAt = DateTimeOffset.UtcNow.AddMinutes(17).ToLocalTime()
            },
            new DbModels.SpotStatusHistory
            {
                SpotId = spotId,
                CreatedByUser = dispo,
                SpotStatus = Status.Spot.Accepted,
                //CreatedAt = DateTimeOffset.UtcNow.AddMinutes(19).ToLocalTime()
            }
        };
        return initSpotStatusHistory;
    }

    public static DbModels.Offer[] Offers()
    {
        var initOffers = new[]
        {
            new DbModels.Offer
            {
                SpotId = spotId,
                UserId = carrier.Id,
                CreatedByUser = carrier,
                CreatedForUser = carrier,
                Value = 550,
                //ValidTill = DateTimeOffset.UtcNow.AddHours(1)
            },
            new DbModels.Offer
            {
                SpotId = spotId,
                UserId = carrier.Id,
                CreatedByUser = dispo,
                CreatedForUser = carrier,
                Value = 400,
                ValidTill = DateTimeOffset.Now.AddHours(1.5),
                Remarks = "Negotiation for 30 min with target 400e", 
                Status = Status.Offer.ToNegotiation
            },
            new DbModels.Offer
            {
                Id = winnerOfferId,
                SpotId = spotId,
                UserId = carrier.Id,
                CreatedByUser = carrier,
                CreatedForUser = carrier,
                Value = 450,
                //ValidTill = DateTimeOffset.UtcNow.AddHours(1), 
                Remarks = "450e min.", 
                Status = Status.Offer.Negotiated,
                IsWinnerOffer = true
            }
        };
        return initOffers;
    }
}