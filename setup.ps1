# KindHelp — one-shot local setup for Windows
# Run from an ELEVATED PowerShell (right-click PowerShell -> Run as Administrator):
#   cd D:\KindHelp\KindHelp
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
#   .\setup.ps1
#
# It is safe to re-run. Each step is idempotent.

param(
    [string]$PgSuperPassword,
    [string]$KindHelpDbPassword = "kindhelp_dev_2026"
)

$ErrorActionPreference = "Stop"

function Write-Step($msg)  { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "  ok  $msg" -ForegroundColor Green }
function Write-Info($msg)  { Write-Host "  ..  $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "  !!  $msg" -ForegroundColor Red }

# ----- 0. Sanity checks -------------------------------------------------------
Write-Step "Checking prerequisites"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Fail "This script must run as Administrator. Re-launch PowerShell with 'Run as Administrator' and try again."
    exit 1
}
Write-Ok "Running as Administrator"

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Fail "winget is not available. Install 'App Installer' from the Microsoft Store, then re-run."
    exit 1
}
Write-Ok "winget available"

function Refresh-Path {
    $env:Path =
        [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
        [System.Environment]::GetEnvironmentVariable("Path","User")
}

# ----- 1. .NET 8 SDK ----------------------------------------------------------
Write-Step "Ensuring .NET 8 SDK is installed"

$dotnetOk = $false
try {
    $v = & dotnet --list-sdks 2>$null
    if ($v -match "^8\.") { $dotnetOk = $true }
} catch {}

if ($dotnetOk) {
    Write-Ok ".NET 8 SDK already present"
} else {
    Write-Info "Installing Microsoft.DotNet.SDK.8 via winget..."
    winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements --silent
    Refresh-Path
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Fail "dotnet not found on PATH after install. Open a new PowerShell window and re-run setup.ps1."
        exit 1
    }
    Write-Ok ".NET 8 SDK installed"
}

# ----- 2. PostgreSQL ----------------------------------------------------------
Write-Step "Ensuring PostgreSQL is installed"

function Find-Psql {
    $base = "C:\Program Files\PostgreSQL"
    if (-not (Test-Path $base)) { return $null }
    Get-ChildItem $base -Directory | Sort-Object Name -Descending | ForEach-Object {
        $p = Join-Path $_.FullName "bin\psql.exe"
        if (Test-Path $p) { return $p }
    } | Select-Object -First 1
}

$psql = Find-Psql
if ($psql) {
    Write-Ok "PostgreSQL found at $psql"
} else {
    Write-Info "Installing PostgreSQL 16 via winget..."
    Write-Info "When the installer prompts for the 'postgres' superuser password, choose one and remember it."
    winget install --id PostgreSQL.PostgreSQL.16 -e --accept-source-agreements --accept-package-agreements
    Refresh-Path
    $psql = Find-Psql
    if (-not $psql) {
        Write-Fail "psql.exe was not found after install. Install PostgreSQL manually from https://www.postgresql.org/download/windows/ and re-run."
        exit 1
    }
    Write-Ok "PostgreSQL installed"
}

# Make sure the service is running
$svc = Get-Service -Name "postgresql-*" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($svc -and $svc.Status -ne "Running") {
    Write-Info "Starting service $($svc.Name)..."
    Start-Service $svc.Name
    Write-Ok "Service started"
} elseif ($svc) {
    Write-Ok "Service $($svc.Name) is running"
}

# ----- 3. Database role and database -----------------------------------------
Write-Step "Creating kindhelp role and database"

if (-not $PgSuperPassword) {
    $sec = Read-Host -AsSecureString "Enter the 'postgres' superuser password you set during install"
    $PgSuperPassword = [System.Net.NetworkCredential]::new("", $sec).Password
}

$env:PGPASSWORD = $PgSuperPassword

# COUNT(*) always returns a row, so we never get $null back.
$roleCount = "$(& $psql -U postgres -tAc 'SELECT count(*) FROM pg_roles WHERE rolname=''kindhelp''')".Trim()
if ($roleCount -eq "1") {
    # Role exists from a previous run — reset its password to match what we write into
    # appsettings.Development.json so the two never drift out of sync.
    & $psql -U postgres -c "ALTER USER kindhelp WITH PASSWORD '$KindHelpDbPassword';" | Out-Null
    Write-Ok "Role 'kindhelp' already exists; password resynced to config"
} else {
    & $psql -U postgres -c "CREATE USER kindhelp WITH PASSWORD '$KindHelpDbPassword';" | Out-Null
    Write-Ok "Role 'kindhelp' created"
}

$dbCount = "$(& $psql -U postgres -tAc 'SELECT count(*) FROM pg_database WHERE datname=''kindhelp''')".Trim()
if ($dbCount -eq "1") {
    Write-Ok "Database 'kindhelp' already exists"
} else {
    & $psql -U postgres -c "CREATE DATABASE kindhelp OWNER kindhelp;" | Out-Null
    Write-Ok "Database 'kindhelp' created"
}

Remove-Item Env:PGPASSWORD

# ----- 4. Local connection string --------------------------------------------
Write-Step "Writing local connection string to appsettings.Development.json"

$projDir = Join-Path $PSScriptRoot "src\KindHelp.Web"
$devPath = Join-Path $projDir "appsettings.Development.json"

$dev = [ordered]@{
    ConnectionStrings = @{
        DefaultConnection = "Host=localhost;Port=5432;Database=kindhelp;Username=kindhelp;Password=$KindHelpDbPassword"
    }
    KindHelp = @{
        # Dev-only admin seed. The committed appsettings.json intentionally leaves these blank;
        # appsettings.Development.json (gitignored) supplies them locally so the first run still works.
        DefaultAdminEmail    = "admin@kindhelp.local"
        DefaultAdminPassword = "ChangeMe!2026"
    }
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
            "Microsoft.EntityFrameworkCore.Database.Command" = "Warning"
        }
    }
    DetailedErrors = $true
}
($dev | ConvertTo-Json -Depth 5) | Set-Content -Path $devPath -Encoding UTF8
Write-Ok "Wrote $devPath"

# ----- 5. dotnet-ef and restore ---------------------------------------------
Write-Step "Restoring NuGet packages and installing dotnet-ef"

Push-Location $projDir
try {
    dotnet restore | Out-Host

    $efInstalled = $false
    try {
        $efv = & dotnet ef --version 2>$null
        if ($LASTEXITCODE -eq 0) { $efInstalled = $true; Write-Ok "dotnet-ef present ($efv)" }
    } catch {}

    if (-not $efInstalled) {
        Write-Info "Installing dotnet-ef global tool..."
        dotnet tool install --global dotnet-ef --version 8.0.4 | Out-Host
        $env:Path += ";$env:USERPROFILE\.dotnet\tools"
    }

    # Explicit build first so any compiler errors are visible — dotnet ef hides them
    # behind a generic "Build failed" message otherwise.
    Write-Info "Building project..."
    dotnet build --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build failed. See the errors above. Once fixed, re-run .\setup.ps1"
        exit 1
    }
    Write-Ok "Build succeeded"

    if (-not (Test-Path "Data\Migrations")) {
        Write-Info "Generating initial EF Core migration..."
        dotnet ef migrations add Initial | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "Migration generation failed."
            exit 1
        }
        Write-Ok "Initial migration created"
    } else {
        Write-Ok "EF migrations already exist"
    }
} finally {
    Pop-Location
}

# ----- 6. Done ---------------------------------------------------------------
Write-Step "Setup complete"
Write-Host ""
Write-Host "Next step:" -ForegroundColor Cyan
Write-Host "    .\run.ps1" -ForegroundColor White
Write-Host ""
Write-Host "The first time it boots it will apply migrations and seed a default admin." -ForegroundColor Gray
Write-Host "Default admin (CHANGE BEFORE DEPLOYING):" -ForegroundColor Gray
Write-Host "    email:    admin@kindhelp.local" -ForegroundColor Gray
Write-Host "    password: ChangeMe!2026" -ForegroundColor Gray
Write-Host ""
