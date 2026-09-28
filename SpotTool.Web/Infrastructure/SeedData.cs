using JasperFx;
using Marten;
using Marten.Schema;
using SpotTool.Web.Domain;
using SpotTool.Web.Domain.Types;
using SpotTool.Web.Infrastructure;

namespace SpotTool.Web.Infrastructure;
public class SeedData : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken cancellation)
    {
        // Otwieramy lekką sesję do bazy danych
        // Tutaj IDocumentStore jest potrzebny do mamy konfiguracje wszystkich dokumentów
        await using var session = store.LightweightSession();

        // 1. Sprawdzamy, czy w bazie istnieje już chociaż jeden z tych użytkowników
        var userExists = await session.Query<DbModels.User>().AnyAsync(cancellation);

        // Jeśli dane już tam są, przerywamy seeder, aby nie duplikować wpisów
        if (userExists) return;

        // Przykładowe dane do zasiedlenia bazy
        var initialUsers = InitModels.Users();
        var initSpotStatusHistory = InitModels.SpotStatusHistory();
        var initOffers = InitModels.Offers();
        var initialSpots = InitModels.Spots();

        // Zapisujemy obiekty (Marten sam obsłuży to jako UPSERT)
        session.Store(initialUsers);
        session.Store(initialSpots);
        session.Store(initSpotStatusHistory);
        session.Store(initOffers);

        // Zatwierdzamy zmiany w PostgreSQL
        await session.SaveChangesAsync(cancellation);
    }
}