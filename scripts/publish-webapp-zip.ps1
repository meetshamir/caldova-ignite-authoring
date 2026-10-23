<#
.SYNOPSIS
    Builds the Caldova Retail Ops app and publishes it as webapp.zip to the file-host
    site, which the demo Web App serves via WEBSITE_RUN_FROM_PACKAGE.

.DESCRIPTION
    This script:
      1. Runs `dotnet publish -c Release` against the repo.
      2. Zips the publish output.
      3. Uploads the zip to the file-host site's wwwroot/webapp.zip via the Kudu
         VFS API, authenticating with an Azure Resource Manager access token
         (no secrets are stored in this script or fetched into app settings).

    Run this any time you want to push a new build of the app to the demo URL
    (https://<FileHostSiteName>....azurewebsites.net/webapp.zip). The demo Web App
    (configured with WEBSITE_RUN_FROM_PACKAGE pointing at that URL) picks up new
    content the next time it restarts / syncs. Use -RestartSite to restart the
    demo Web App automatically after the upload.

.PARAMETER ResourceGroup
    Resource group containing the file-host site (and, optionally, the demo Web App).

.PARAMETER FileHostSiteName
    Name of the Web App whose wwwroot serves webapp.zip (the "file host").

.PARAMETER DemoSiteName
    Optional name of the actual demo Web App (the one running WEBSITE_RUN_FROM_PACKAGE).
    If supplied along with -RestartSite, the script restarts it after uploading.

.PARAMETER RepoRoot
    Path to the repo root. Defaults to the parent of this script's folder.

.PARAMETER Configuration
    Build configuration to publish. Defaults to Release.

.PARAMETER RestartSite
    If set (and -DemoSiteName is provided), restarts the demo Web App after upload
    so it reloads WEBSITE_RUN_FROM_PACKAGE content immediately.

.EXAMPLE
    .\publish-webapp-zip.ps1 -ResourceGroup rg-ignite2026-sreagent-demo `
        -FileHostSiteName ignite2026-sreagent-demo `
        -DemoSiteName caldova-webapp -RestartSite
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$FileHostSiteName,

    [string]$DemoSiteName,

    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$Configuration = "Release",

    [switch]$RestartSite
)

$ErrorActionPreference = "Stop"

function Assert-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI ('az') is required and was not found on PATH."
    }
    $account = az account show -o json 2>$null
    if (-not $account) {
        throw "Not logged in to Azure CLI. Run 'az login' first."
    }
}

Assert-AzCli

$publishDir = Join-Path $RepoRoot "publish"
$zipPath = Join-Path ([IO.Path]::GetTempPath()) "webapp.zip"

Write-Host "==> Publishing (Configuration=$Configuration) from $RepoRoot" -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
dotnet publish "$RepoRoot" -c $Configuration -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "==> Zipping publish output to $zipPath" -ForegroundColor Cyan
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$zipInfo = Get-Item $zipPath
Write-Host "==> Package size: $([math]::Round($zipInfo.Length / 1KB, 1)) KB" -ForegroundColor Cyan

Write-Host "==> Acquiring an ARM access token for Kudu authentication" -ForegroundColor Cyan
$token = az account get-access-token --resource "https://management.azure.com" --query accessToken -o tsv
if (-not $token) {
    throw "Failed to acquire an Azure access token. Are you logged in ('az login')?"
}

$scmHost = "$FileHostSiteName.scm.azurewebsites.net"
# Resolve the real scm hostname (handles custom regional suffixes like *-01.westus3-01.azurewebsites.net)
$hostNames = az webapp show -g $ResourceGroup -n $FileHostSiteName --query "enabledHostNames" -o json | ConvertFrom-Json
$scmCandidate = $hostNames | Where-Object { $_ -like "*.scm.*" } | Select-Object -First 1
if ($scmCandidate) {
    $scmHost = $scmCandidate
}

$vfsUrl = "https://$scmHost/api/vfs/site/wwwroot/webapp.zip"
Write-Host "==> Uploading to $vfsUrl" -ForegroundColor Cyan

try {
    $resp = Invoke-WebRequest -Uri $vfsUrl `
        -Headers @{ Authorization = "Bearer $token"; "If-Match" = "*" } `
        -Method Put -InFile $zipPath -ContentType "application/octet-stream" -UseBasicParsing
    Write-Host "==> Upload succeeded (HTTP $($resp.StatusCode))" -ForegroundColor Green
}
catch {
    throw "Upload to Kudu VFS failed: $($_.Exception.Message)"
}

if ($RestartSite -and $DemoSiteName) {
    Write-Host "==> Restarting demo Web App '$DemoSiteName' to pick up the new package" -ForegroundColor Cyan
    az webapp restart -g $ResourceGroup -n $DemoSiteName | Out-Null
    Write-Host "==> Restart requested." -ForegroundColor Green
}

$defaultHostName = az webapp show -g $ResourceGroup -n $FileHostSiteName --query "defaultHostName" -o tsv
Write-Host "==> Done. webapp.zip is live at https://$defaultHostName/webapp.zip" -ForegroundColor Green
