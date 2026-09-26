using FastEndpoints;
using Marten;
using Npgsql.Replication;
using SpotTool.Web;
using SpotTool.Web.Components;
using JasperFx;
using Weasel.Core.Partitioning;
using SpotTool.Web.Db;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//Moje start
builder.Services.AddFastEndpoints();

//Wlaczamy globalna obsluge ProblemDetails
// Wersja minimalistyczna w handlerze (przy włączonej konfiguracji globalnej):
// await Send.ResultAsync(TypedResults.Problem(
//     detail: "Nie można zmodyfikować oferty, ponieważ przetarg został już zamknięty.",
//     title: "Przetarg nieaktywny",
//     statusCode: 409
// ));
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        // Każdy błąd w aplikacji dostanie automatycznie ścieżkę i TraceId
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
        ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier);
    };
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("SpotToolMartenDB")!);
    opts.UseSystemTextJsonForSerialization(enumStorage: Weasel.Core.EnumStorage.AsString);

    // Marten sam zadba o tworzenie i delikatne aktualizacje struktur w Postgresie
    if (builder.Environment.IsDevelopment())
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
}).InitializeWith(new SeedData()); 

builder.Services.AddWebDependencies();

//Moje end

var app = builder.Build();

// Configure the HTTP request pipeline. 
//Do sprawdzenia!
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();
//moje
// ========================================================
// 2. WŁĄCZENIE GLOBALNEGO HANDLERA WYJĄTKÓW (MIDDLEWARE)
// ========================================================
// Ta linijka przechwyci każdy błąd (np. brak połączenia z Postgres/Marten)
// i zamiast "białej strony" wygeneruje czysty, bezpieczny JSON z kodem 500.
app.UseExceptionHandler(); 

app.UseFastEndpoints(c => c.Errors.UseProblemDetails());
// End moje
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
