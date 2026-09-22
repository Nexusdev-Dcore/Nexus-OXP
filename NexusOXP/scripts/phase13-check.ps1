$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $root "NexusOXP.csproj"
$solutionFile = Join-Path $root "NexusOXP.sln"
$appSettingsFile = Join-Path $root "appsettings.json"
$launchSettingsFile = Join-Path $root "Properties\launchSettings.json"

Write-Host "=== Phase 13 validation ===" -ForegroundColor Cyan

if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found: $solutionFile"
}

if (-not (Test-Path $appSettingsFile)) {
    throw "appsettings.json not found: $appSettingsFile"
}

if (-not (Test-Path $launchSettingsFile)) {
    throw "launchSettings.json not found: $launchSettingsFile"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "dotnet SDK is not installed or not on PATH. Install .NET 10 SDK first."
}

Write-Host "dotnet found: $($dotnet.Source)" -ForegroundColor Green

$appSettings = Get-Content $appSettingsFile -Raw | ConvertFrom-Json
$connectionString = $appSettings.ConnectionStrings.DefaultConnection

if (-not $connectionString) {
    Write-Warning "DefaultConnection is missing from appsettings.json."
} else {
    Write-Host "DB connection string: $connectionString" -ForegroundColor Yellow
}

$launchSettings = Get-Content $launchSettingsFile -Raw | ConvertFrom-Json
Write-Host "Expected local URLs:" -ForegroundColor Cyan
foreach ($profile in $launchSettings.profiles.PSObject.Properties) {
    $url = $profile.Value.applicationUrl
    if ($url) {
        Write-Host " - $($profile.Name): $url" -ForegroundColor Green
    }
}

Write-Host "" 
Write-Host "Next step: run Start-NexusOXP.ps1" -ForegroundColor Cyan
