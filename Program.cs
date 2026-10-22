// Caldova Retail Ops - ASP.NET Core Web API.
//
// Deployed to Azure App Service via Run From Package (Run From Zip). A
// static HTML/CSS/JS frontend lives in wwwroot and calls these JSON
// endpoints.

using CaldovaRetailOps.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RecommendationEngine>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<ReportingService>();
var app = builder.Build();

const string Service = "caldova-retail-ops";
var startedAt = DateTimeOffset.UtcNow;

app.Use(async (context, next) =>
{
    var started = DateTimeOffset.UtcNow;
    await next();
    var elapsedMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds;
    Console.WriteLine($"caldova_request service={Service} method={context.Request.Method} path={context.Request.Path} status={context.Response.StatusCode} ms={elapsedMs:F0}");
});

app.UseDefaultFiles();
app.UseStaticFiles();

static bool IsFlagOn(string name) =>
    Environment.GetEnvironmentVariable(name) is "1" or "true";

// ---------------------------------------------------------------------------
// API routes
// ---------------------------------------------------------------------------

app.MapGet("/api/health", () => Results.Json(new
{
    status = "ok",
    service = Service,
    startedAt = startedAt.ToString("o"),
    timestamp = DateTimeOffset.UtcNow.ToString("o")
}));

app.MapGet("/api/status", () => Results.Json(new
{
    service = Service,
    uptimeSeconds = (int)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    dotnetVersion = Environment.Version.ToString(),
    instance = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") is { Length: > 0 } id ? id[..Math.Min(8, id.Length)] : "local",
    flags = new
    {
        BreakMyWebApp = Environment.GetEnvironmentVariable("BreakMyWebApp")
    }
}));

app.MapGet("/api/checkout", () =>
{
    // Demo scenario: set the App Setting BreakMyWebApp=1 to make checkout
    // fail with an HTTP 500, without any code change or redeploy. This is
    // the one purely-configuration-driven scenario in this app.
    if (IsFlagOn("BreakMyWebApp"))
    {
        Console.Error.WriteLine($"caldova_checkout_failed service={Service} reason=BreakMyWebApp_app_setting_enabled");
        return Results.Json(new { error = "checkout_failed", detail = "An unexpected error occurred while processing your order." }, statusCode: 500);
    }

    return Results.Json(new { orderId = "CAL-10482", items = 3, total = 142.50, status = "confirmed" });
});

app.MapGet("/api/search", (HttpRequest request, SearchService search) =>
    Results.Json(search.Search(request.Query["page"].ToString())));

app.MapGet("/api/reports", (HttpRequest request, ReportingService reporting) =>
{
    var day = int.TryParse(request.Query["day"].ToString(), out var parsedDay) ? parsedDay : 1;
    return Results.Json(reporting.GetDailySales(day));
});

app.MapGet("/api/recommendations", (HttpRequest request, RecommendationEngine engine) =>
{
    var customerId = request.Query["customerId"].ToString();
    if (string.IsNullOrWhiteSpace(customerId))
    {
        customerId = "RETURNING-0001";
    }
    return Results.Json(engine.GetRecommendations(customerId));
});

app.MapFallbackToFile("index.html");

app.Run();
