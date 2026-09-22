using Marten;
using Marten.Schema;

namespace SpotTool.Web.Features.Shared;
public class SeedData : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        // Otwieramy lekką sesję do bazy danych
        await using var session = store.LightweightSession();

        // 1. Sprawdzamy, czy w bazie istnieje już chociaż jeden z tych użytkowników
        var userExists = await session.Query<DbModels.User>()
            .AnyAsync(x => x.Email == "arkadiusz.mikulec@gmail.com", cancellation);

        // Jeśli dane już tam są, przerywamy seeder, aby nie duplikować wpisów
        if (userExists) return;

        // Przykładowe dane do zasiedlenia bazy
        var initialUsers = new[]
        {
            new DbModels.User { Email = "arkadiusz.mikulec@gmail.com" },
            new DbModels.User { Email = "amikulec@sostmeier.pl" },
            new DbModels.User { Email = "biuro@e-site.pl" }
        };

        var initialSpots = new[]
        {
            new DbModels.Spot { Description = "Spot1", TargetedCost = 100, UserId = initialUsers[0].Id }
        };

        // Zapisujemy obiekty (Marten sam obsłuży to jako UPSERT)
        session.Store(initialUsers);
        session.Store(initialSpots);

        // Zatwierdzamy zmiany w PostgreSQL
        await session.SaveChangesAsync(cancellation);
    }
}