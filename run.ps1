# KindHelp — run locally
# Run from PowerShell (no admin required after setup.ps1 has been run once):
#   .\run.ps1
#
# Opens http://localhost:5080 in your default browser once the server is up.

$ErrorActionPreference = "Stop"

$projDir = Join-Path $PSScriptRoot "src\KindHelp.Web"
if (-not (Test-Path $projDir)) {
    Write-Host "Project not found at $projDir" -ForegroundColor Red
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host ".NET SDK not on PATH. Run .\setup.ps1 first." -ForegroundColor Red
    exit 1
}

# Open the browser after a short delay so Kestrel has time to start.
$browserJob = Start-Job -ScriptBlock {
    Start-Sleep -Seconds 4
    Start-Process "http://localhost:5080"
}

Push-Location $projDir
try {
    # Force the Development environment so appsettings.Development.json (which holds
    # the real DB connection string for local dev) actually gets loaded.
    $env:ASPNETCORE_ENVIRONMENT = "Development"

    Write-Host "Starting KindHelp at http://localhost:5080 (ASPNETCORE_ENVIRONMENT=Development) ..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop." -ForegroundColor Gray
    dotnet run --no-launch-profile --urls "http://localhost:5080"
} finally {
    Pop-Location
    Receive-Job $browserJob -ErrorAction SilentlyContinue | Out-Null
    Remove-Job $browserJob -ErrorAction SilentlyContinue
}
