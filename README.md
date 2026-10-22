# Caldova Retail Ops

A minimal web app used as an Azure App Service SRE Agent demo lab.

## Architecture

- **Backend**: ASP.NET Core Web API (C#, `net8.0`), minimal APIs, no NuGet
  package references beyond the ASP.NET Core shared framework.
- **Frontend**: Static HTML/CSS/JS served from `wwwroot/` (no npm, no build
  step) calling the backend's JSON endpoints.
- Both are hosted together as a single ASP.NET Core app, deployed as one
  Azure App Service site via **Run From Package (Run From Zip)**. The
  running site's code cannot be modified in place — the only way to ship a
  fix is to merge a pull request and publish a new package.

## Running locally

```powershell
dotnet run
```

Then open `http://localhost:5000` (or the URL printed on startup).

## API endpoints

| Route                      | Description                          |
|-----------------------------|----------------------------------------|
| `GET /api/health`           | Liveness check                        |
| `GET /api/status`           | Uptime, runtime info, active flags    |
| `GET /api/checkout`         | Simulated checkout                    |
| `GET /api/search?page=`     | Paginated catalog search              |
| `GET /api/reports?day=`     | Daily sales report (1=Mon ... 7=Sun)  |
| `GET /api/recommendations?customerId=` | Personalized recommendations |

## Incident scenario 1 — configuration (App Setting)

| App Setting      | Value | Effect                                       |
|-------------------|------|-----------------------------------------------|
| `BreakMyWebApp`   | `1`  | `GET /api/checkout` returns HTTP 500          |

This is the one purely config-driven scenario in the lab: set the App
Setting in the Azure Portal (or CLI) to break checkout, and remove it to
fix. Good as a warm-up incident — the agent should recognize this is a
configuration regression (e.g. correlate with a recent App Setting change)
and remediate without touching code.

## Incident scenarios 2-4 — real code bugs (require a PR to fix)

These are not toggled by any setting — they are always present in the
deployed code and only reproduce for specific, realistic inputs. Because the
site runs from a fixed, immutable package, the agent cannot fix these by
changing configuration: it has to read the source in GitHub, find the root
cause, and open a pull request. (Merging the PR and publishing a new
package is a separate, manual step for this lab — see "Publishing a new
package" below.)

### Scenario 2 — New customers can't load recommendations

**Symptom:** Customers who just created an account get an error loading
their personalized recommendations, while existing customers are fine.

**Reproduce:**
```
GET /api/recommendations?customerId=NEW-123
```
(Any customer ID starting with `NEW-` reproduces it; try `customerId=RETURNING-0001`
first to see the healthy path.)

**Investigation path:** Check the exception captured in App Service logs /
Application Insights for the failing request, then look at
`Services/RecommendationEngine.cs` on GitHub to find why this particular
customer segment fails while others succeed.

### Scenario 3 — Search breaks on an unexpected page value

**Symptom:** Product search works for ordinary pagination, but some
real-world traffic to the search endpoint results in a server error.

**Reproduce:**
```
GET /api/search?page=two
```
(Compare with `page=1`, which works.)

**Investigation path:** Check the exception type and message, then review
`Services/SearchService.cs` to find the missing input validation.

### Scenario 4 — End-of-week sales report crashes

**Symptom:** The daily sales report works for most days of the week, but
one specific, perfectly valid day consistently errors out.

**Reproduce:**
```
GET /api/reports?day=7
```
(Compare with `day=1` through `day=6`, which work.)

**Investigation path:** Check the exception, then review
`Services/ReportingService.cs` for how the day parameter is used to look up
sales data.

## Azure deployment

| Resource | Name |
|----------|------|
| Resource group | `rg-ignite2026-sreagent-demo` |
| Demo Web App (runs the app, `WEBSITE_RUN_FROM_PACKAGE`) | `ignite2026-sreagent-app` |
| File-host Web App (serves `webapp.zip` from its `wwwroot`) | `ignite2026-sreagent-demo` |

The demo Web App has its `WEBSITE_RUN_FROM_PACKAGE` App Setting pointed at
`https://ignite2026-sreagent-demo-cfgsgac8h8abh2d9.westus3-01.azurewebsites.net/webapp.zip`,
which is hosted as a plain file in the file-host site's `wwwroot/`. The demo
app's own code is therefore never writable in place — it only reads a zip
over HTTPS — which is what makes the "agent can't just hot-patch the
running site" constraint real for this lab.

## Publishing a new package

`scripts/publish-webapp-zip.ps1` builds this repo, zips the publish output,
and uploads it to the file-host site's `wwwroot/webapp.zip` via the Kudu VFS
API (authenticated with your own `az login` session — no secrets are stored
anywhere). Use it after merging a fix PR to actually ship the change:

```powershell
.\scripts\publish-webapp-zip.ps1 `
    -ResourceGroup rg-ignite2026-sreagent-demo `
    -FileHostSiteName ignite2026-sreagent-demo `
    -DemoSiteName ignite2026-sreagent-app `
    -RestartSite
```

`-RestartSite` restarts the demo Web App afterward so it reloads the new
package immediately; omit it if you'd rather let the app pick it up on its
own next restart/scale event.
