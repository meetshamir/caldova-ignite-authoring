// Caldova Retail Ops - dependency-free ASP.NET Core Web API.
//
// Designed for Azure App Service deployed via Run From Package (Run From
// Zip): the published output needs no NuGet packages beyond the ASP.NET
// Core shared framework. A static HTML/CSS/JS frontend lives in wwwroot
// and calls these JSON endpoints.
//
// Several endpoints read App Settings (environment variables) so an SRE
// Agent demo can "break" and "fix" the site purely through Azure
// configuration changes, without ever touching the code in this repo.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string Service = "caldova-retail-ops";
var startedAt = DateTimeOffset.UtcNow;

// In-memory store used by the memory-leak demo scenario. Intentionally never
// cleared so it can simulate unbounded memory growth over time.
var leakedBuffers = new List<byte[]>();

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
    reportsCached = leakedBuffers.Count,
    flags = new
    {
        BreakMyWebApp = Environment.GetEnvironmentVariable("BreakMyWebApp"),
        SlowResponseMs = Environment.GetEnvironmentVariable("SlowResponseMs"),
        EnableMemoryLeak = Environment.GetEnvironmentVariable("EnableMemoryLeak"),
        DisableRecommendations = Environment.GetEnvironmentVariable("DisableRecommendations")
    }
}));

app.MapGet("/api/checkout", () =>
{
    // Demo scenario: set the App Setting BreakMyWebApp=1 to make checkout
    // fail with an HTTP 500, without any code change or redeploy.
    if (IsFlagOn("BreakMyWebApp"))
    {
        Console.Error.WriteLine($"caldova_checkout_failed service={Service} reason=BreakMyWebApp_app_setting_enabled");
        return Results.Json(new { error = "checkout_failed", detail = "An unexpected error occurred while processing your order." }, statusCode: 500);
    }

    return Results.Json(new { orderId = "CAL-10482", items = 3, total = 142.50, status = "confirmed" });
});

app.MapGet("/api/search", async () =>
{
    // Demo scenario: set the App Setting SlowResponseMs=<milliseconds> to
    // make product search respond slowly, simulating a performance
    // degradation incident that an SRE Agent can diagnose via latency
    // telemetry.
    var delayMs = int.TryParse(Environment.GetEnvironmentVariable("SlowResponseMs"), out var ms) ? ms : 0;
    if (delayMs > 0)
    {
        await Task.Delay(delayMs);
    }

    return Results.Json(new
    {
        delayMs,
        products = new[]
        {
            new { sku = "CAL-001", name = "Trail Runner Boot", price = 89.00 },
            new { sku = "CAL-014", name = "Alpine Shell Jacket", price = 210.00 },
            new { sku = "CAL-027", name = "Summit Pack 28L", price = 129.00 }
        }
    });
});

app.MapGet("/api/reports", () =>
{
    // Demo scenario: set the App Setting EnableMemoryLeak=1 so every request
    // retains a buffer that is never released, simulating a slow memory leak
    // that climbs worker memory over time (visible in App Service metrics).
    if (IsFlagOn("EnableMemoryLeak"))
    {
        leakedBuffers.Add(new byte[5 * 1024 * 1024]); // retain 5MB per request
    }

    return Results.Json(new
    {
        periodDays = 7,
        orders = 1204,
        revenue = 86310.00,
        reportsCached = leakedBuffers.Count
    });
});

app.MapGet("/api/recommendations", () =>
{
    // Demo scenario: set the App Setting DisableRecommendations=1 to
    // simulate a feature-flag misconfiguration where a route starts
    // returning HTTP 404.
    if (IsFlagOn("DisableRecommendations"))
    {
        return Results.Json(new { error = "not_found", detail = "This feature is currently disabled." }, statusCode: 404);
    }

    return Results.Json(new
    {
        items = new[]
        {
            new { sku = "CAL-055", name = "Insulated Gloves", price = 32.00 },
            new { sku = "CAL-061", name = "Wool Base Layer", price = 48.00 }
        }
    });
});

app.MapFallbackToFile("index.html");

app.Run();
