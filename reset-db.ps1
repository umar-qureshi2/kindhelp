# Wipe the local KindHelp database and regenerate from current model.
# DESTRUCTIVE — only for local dev. Run from elevated PowerShell:
#   .\reset-db.ps1

param([string]$KindHelpDbPassword = "kindhelp_dev_2026")

$ErrorActionPreference = "Stop"

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  ok  $m" -ForegroundColor Green }
function Info($m) { Write-Host "  ..  $m" -ForegroundColor Yellow }
function Fail($m) { Write-Host "  !!  $m" -ForegroundColor Red }

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Fail "Run elevated."
    exit 1
}

# Find psql
$base = "C:\Program Files\PostgreSQL"
$pgDir = Get-ChildItem $base -Directory | Sort-Object Name -Descending | Select-Object -First 1
$psql = Join-Path $pgDir.FullName "bin\psql.exe"
if (-not (Test-Path $psql)) { Fail "psql not found"; exit 1 }

Step "Dropping the 'kindhelp' database"
$sec = Read-Host -AsSecureString "Enter 'postgres' superuser password"
$env:PGPASSWORD = [System.Net.NetworkCredential]::new("", $sec).Password

& $psql -U postgres -h localhost -d postgres -c "DROP DATABASE IF EXISTS kindhelp WITH (FORCE);" | Out-Host
& $psql -U postgres -h localhost -d postgres -c "CREATE DATABASE kindhelp OWNER kindhelp;" | Out-Host
Ok "Database recreated"

Remove-Item Env:PGPASSWORD

Step "Removing old EF migrations"
$migDir = Join-Path $PSScriptRoot "src\KindHelp.Web\Migrations"
if (Test-Path $migDir) {
    Remove-Item $migDir -Recurse -Force
    Ok "Removed $migDir"
}

Step "Restoring + regenerating initial migration"
Push-Location (Join-Path $PSScriptRoot "src\KindHelp.Web")
try {
    dotnet restore | Out-Host
    dotnet build --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "Build failed."; exit 1 }
    dotnet ef migrations add Initial | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "Migration generation failed."; exit 1 }
    Ok "Migration created"
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Database wiped and ready. Run:" -ForegroundColor Cyan
Write-Host "    .\run.ps1" -ForegroundColor White
