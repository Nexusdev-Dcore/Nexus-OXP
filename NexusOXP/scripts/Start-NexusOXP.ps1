$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $root "NexusOXP.csproj"
$solutionFile = Join-Path $root "NexusOXP.sln"

if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found: $solutionFile"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "dotnet SDK is not installed or not on PATH. Install .NET 10 SDK first."
}

Write-Host "=== NexusOXP startup ===" -ForegroundColor Cyan
Write-Host "Restoring packages..." -ForegroundColor Yellow
& dotnet restore $solutionFile

Write-Host "Building solution..." -ForegroundColor Yellow
& dotnet build $solutionFile --nologo

Write-Host "Starting web app on the configured local profile..." -ForegroundColor Yellow
& dotnet run --project $projectFile --launch-profile https
