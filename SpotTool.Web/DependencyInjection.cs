using Marten;
using JasperFx;
using FastEndpoints;
using SpotTool.Web.Features.Shared.Spots;
using SpotTool.Web.Domain;
using SpotTool.Web.Infrastructure;

namespace SpotTool.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebDependencies(this IServiceCollection services)
    {
        services.AddSingleton<ISpotService, SpotServiceInMemory>();
        
        return services;
    }

    public static IServiceCollection AddMartenDependencie(this IServiceCollection services, 
        IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        services.AddMarten(opts =>
        {
            opts.Connection(configuration.GetConnectionString("SpotToolMartenDB")!);
            opts.UseSystemTextJsonForSerialization(enumStorage: Weasel.Core.EnumStorage.AsString);

            // Marten sam zadba o tworzenie i delikatne aktualizacje struktur w Postgresie
            if (webHostEnvironment.IsDevelopment())
            {
                // Deweloper ma mieć wygodę, baza dostosowuje się do kodu w locie
                opts.AutoCreateSchemaObjects = AutoCreate.All;
            }
            else
            {
                // Na produkcji pełna blokada automatycznych migracji runtime
                opts.AutoCreateSchemaObjects = AutoCreate.None;
            }
            
            opts.Schema.For<DbModels.Spot>().Index(x => x.UserId);

            opts.Schema.For<DbModels.SpotStatusHistory>().Index(x => x.SpotId);

            opts.Schema.For<DbModels.Offer>().Index(x => x.SpotId);
            opts.Schema.For<DbModels.Offer>().Index(x => x.UserId);


            // 1. Nakazujemy Martenowi wyciągnąć pole z JSONB do fizycznej kolumny w Postgresie
            // opts.Schema.For<DbModels.Spot>().Duplicate(x => x.CreatedAt);

            // // // 2. Informujemy Martena, że tabela ma być partycjonowana po tej kolumnie (w ujęciu rocznym lub miesięcznym)
            // opts.Schema.For<DbModels.Spot>().PartitionOn(x => x.CreatedAt, x => 
            // {
            //     // Tutaj konfigurujesz konkretny typ partycjonowania PostgreSQL, 
                
            //     // np. automatyczne okno czasowe (Rolling Range):
            //     //Partycje beda miesieczne. Tworzone na 3 misiace do przodu, ale wszystkie starsze niz 12 miesiecy beda usuwane. (Dane równiez)
            //     //x.ByRollingRange(PartitionPeriod.Month, periodsAhead: 3, periodsBehind: 12);

            //     // By range dodaje na sztywno partycje, ale trzeba dbac o to aby zaktualizowac applikacje jak zakresy sie konczą
            //     // Kiepsko 
            // x.ByRange()
                //     .AddRange("spots_2026_q1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local), new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Local))
                //     .AddRange("spots_2026_q2", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Local), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Local))
                //     .AddRange("spots_2026_q3", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Local), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Local))
                //     .AddRange("spots_2026_q4", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Local), new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Local));

            //     //Marten szykuje tabele pod partycjonowanie 
            //     //ale to administrator musi nimi zarządzać
            //     //x.ByExternallyManagedRangePartitions();;
            // });

            //ApplyAllDatabaseChangesOnStartup 
            // wymusi aktualizację bazy przy stacie a nie podczas pirwszego dodania rekordu
            //Przydatne w dev oraz małej produkcji kiedy masz ustawiony opts.AutoCreateSchemaObjects = AutoCreate.All;
            //Zastanowić sie czy urzywać w produkcji

            //InitializeWith
            //Inicjalizuje baze jakimis danymy wywoluje w tle ApplyAllDatabaseChangesOnStartup; 
        })
        .UseLightweightSessions()
        .InitializeWith(new SeedData());
         
        return services;
    }
}