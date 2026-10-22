# Caldova Retail Ops

A minimal, dependency-free web app used as an Azure App Service SRE Agent
demo lab. It mirrors the look and feel of
[saziz_microsoft/caldova-ignite-authoring](https://github.com/saziz_microsoft/caldova-ignite-authoring).

## Architecture

- **Backend**: ASP.NET Core Web API (C#, `net8.0`), minimal APIs, no NuGet
  package references beyond the ASP.NET Core shared framework.
- **Frontend**: Static HTML/CSS/JS served from `wwwroot/` (no npm, no build
  step) calling the backend's JSON endpoints.
- Both are hosted together as a single ASP.NET Core app, so the whole thing
  deploys as one Azure App Service site — a good fit for **Run From
  Package / Run From Zip** deployments.

## Running locally

```powershell
dotnet run
```

Then open `http://localhost:5000` (or the URL printed on startup).

## API endpoints

| Route                     | Description                         |
|---------------------------|--------------------------------------|
| `GET /api/health`         | Liveness check                      |
| `GET /api/status`         | Uptime, runtime info, active flags  |
| `GET /api/checkout`       | Simulated checkout                  |
| `GET /api/search`         | Simulated product search            |
| `GET /api/reports`        | Simulated sales report              |
| `GET /api/recommendations`| Simulated recommendations           |

## Break-scenario App Settings

These environment variables (Azure App Service **Application Settings**)
let an SRE Agent "break" and "fix" the site purely through configuration —
no code change or redeploy required (and none is possible, since the app is
deployed via Run From Zip).

| App Setting              | Value    | Effect                                                              |
|---------------------------|---------|-----------------------------------------------------------------------|
| `BreakMyWebApp`           | `1`     | `GET /api/checkout` returns HTTP 500                                  |
| `SlowResponseMs`          | `<ms>`  | `GET /api/search` sleeps for `<ms>` before responding (latency demo)  |
| `EnableMemoryLeak`        | `1`     | `GET /api/reports` retains a 5MB buffer per call, never freed (memory growth demo) |
| `DisableRecommendations`  | `1`     | `GET /api/recommendations` returns HTTP 404 (feature-flag misconfiguration demo) |

## Other easy, impactful SRE Agent demo scenario ideas

All of these can be set up/reset purely through Azure configuration (no code
change), which matches the "GitHub connected but read-only, Run From Zip"
constraint:

1. **Scale down to 1 instance under load** — reduce instance count or disable
   autoscale, then generate traffic, to show the agent diagnosing
   throttling/high CPU and recommending a scale-out fix.
2. **Always On disabled** — turn off Always On and show cold-start/idle
   timeout symptoms the agent can detect and fix via config.
3. **Bad deployment slot swap** — stage a "broken" slot (e.g. with
   `BreakMyWebApp=1` set on staging) and swap it into production; the agent
   can diagnose and swap back without any code change.
4. **IP / access restriction misconfiguration** — add an access restriction
   rule that blocks expected traffic (e.g. blocks all), producing 403s the
   agent must find and correct in networking config.
5. **Wrong/stale App Setting pointing at a dependency** — e.g. an app setting
   like `DownstreamApiUrl` pointed at an unreachable host, producing 502/504s
   that look like a bad deployment but are fixed by correcting one setting.
6. **Diagnostic logging turned off** — disable application logging/App
   Service logs, so initial investigation requires the agent to first enable
   diagnostics before it can see what's wrong (a very realistic first step).
7. **Deployment slot stuck/misconfigured `WEBSITE_RUN_FROM_PACKAGE`** — point
   the setting at an invalid or old package URL to simulate a "stuck on old
   version" incident fixed by configuration alone.
8. **Health check path misconfigured** — point App Service's Health Check
   path setting at a route that 404s (e.g. `/api/does-not-exist`), causing
   healthy instances to be marked unhealthy/rotated out.
9. **TLS/HTTPS Only toggle or minimum TLS version mismatch** — flip `HTTPS
   Only` or minimum TLS version to a value callers can't satisfy, producing
   client connection failures diagnosable purely from config.
10. **Restart/stop the site** — simplest possible scenario: stop the App
    Service and have the agent notice and restart it, good as a warm-up
    before the more diagnostic scenarios above.

Each of the four already-implemented flags plus these ten ideas can be
toggled from the Azure Portal, CLI, or ARM/Bicep in seconds and reset the
same way, which keeps the demo lab fast to set up and fast to reset between
runs.
