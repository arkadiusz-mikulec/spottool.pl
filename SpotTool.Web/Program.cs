using FastEndpoints;
using Marten;
using Npgsql.Replication;
using SpotTool.Web;
using SpotTool.Web.Components;
using JasperFx;
using Weasel.Core.Partitioning;

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

//builder.Services.AddMartenDependencie(builder.Configuration, builder.Environment);
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
