using Marten;
using Marten.Schema;
using SpotTool.Web.Domain;

namespace SpotTool.Web.Db;
public class SeedData : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        // Otwieramy lekką sesję do bazy danych
        await using var session = store.LightweightSession();

        // 1. Sprawdzamy, czy w bazie istnieje już chociaż jeden z tych użytkowników
        var userExists = await session.Query<DbModels.User>()
            .AnyAsync(cancellation);

        // Jeśli dane już tam są, przerywamy seeder, aby nie duplikować wpisów
        if (userExists) return;

        // Przykładowe dane do zasiedlenia bazy
        var initialUsers = new[]
        {
            new DbModels.User { 
                ContactDetails = new DbModels.ContactPersonDetail("Arek", "+48881250136", "arkadiusz.mikulec@gmail.com"),  
                Role = Roles.User.Admin,
                UserStatus = Status.User.Ok
            },
            new DbModels.User { 
                ContactDetails = new DbModels.ContactPersonDetail("Arek", "+48881250000", "amikulec@sostmeier.pl"),  
                Role = Roles.User.Disponent,
                UserStatus = Status.User.Ok
            },
            new DbModels.User { 
                ContactDetails = new DbModels.ContactPersonDetail("Biuro", "+48881250111", "biuro@e-site.pl"),  
                Role = Roles.User.Carrier,
                UserStatus = Status.User.Ok
            },
        };

        var carrier = new DbModels.UserSnapShot(initialUsers[2].Id, initialUsers[2].Role, initialUsers[2].UserStatus, initialUsers[2].ContactDetails!.Email);
        var dispo = new DbModels.UserSnapShot(initialUsers[0].Id, initialUsers[0].Role, initialUsers[0].UserStatus, initialUsers[0].ContactDetails!.Email);
        Guid spotId = Guid.CreateVersion7();
        Guid winnerOfferId = Guid.CreateVersion7();
        

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
                SpotStatus = Status.Spot.DuringBidding,
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
                SpotStatus = Status.Spot.DuringNegotiation,
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
                SpotStatus = Status.Spot.ManuallyAccepted,
                //CreatedAt = DateTimeOffset.UtcNow.AddMinutes(19).ToLocalTime()
            }
        };

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
                ValidTill = DateTimeOffset.UtcNow.AddHours(1.5).ToLocalTime(),
                Remarks = "Negotiation for 30 min with target 400e", 
                OfferStatus = Status.Offer.Negotiation
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
                OfferStatus = Status.Offer.Negotiated,
                IsWinnerOffer = true
            }
        };

        var initialSpots = new[]
        {
            new DbModels.Spot {
                Id = spotId, 
                Description = "Spot1", TargetedCost = 400, UserId = initialUsers[0].Id, ContactDetails = initialUsers[0].ContactDetails!, 
                CurrentStatus = Status.Spot.ManuallyAccepted,
                CreatedByUser = dispo,
                WinnerOfferSnapShot = new DbModels.OfferSnapShot(
                    winnerOfferId, 
                    carrier, 
                    450, 
                    DateTimeOffset.UtcNow.ToLocalTime(), 
                    Status.Offer.Negotiated, 
                    "450e min.", 
                    DateTimeOffset.UtcNow.AddHours(2).ToLocalTime(),
                    Currency.Code.EUR
                )
            }
        };

        // Zapisujemy obiekty (Marten sam obsłuży to jako UPSERT)
        session.Store(initialUsers);
        session.Store(initialSpots);
        session.Store(initSpotStatusHistory);
        session.Store(initOffers);

        // Zatwierdzamy zmiany w PostgreSQL
        await session.SaveChangesAsync(cancellation);
    }
}