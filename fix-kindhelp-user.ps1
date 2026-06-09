# Diagnose and fix the 'kindhelp' PostgreSQL role's password.
# Run from PowerShell (admin not required):
#   .\fix-kindhelp-user.ps1
#
# What it does:
#   1. Finds the installed PostgreSQL version and shows you the path.
#   2. Prompts for the 'postgres' superuser password.
#   3. Resets the 'kindhelp' role's password to match appsettings.Development.json.
#   4. Tests a real connection as 'kindhelp' so we know it actually works.

$ErrorActionPreference = "Stop"

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  ok  $m" -ForegroundColor Green }
function Fail($m) { Write-Host "  !!  $m" -ForegroundColor Red }
function Info($m) { Write-Host "  ..  $m" -ForegroundColor Yellow }

# --- 1. Find psql ---
Step "Locating psql.exe"
$base = "C:\Program Files\PostgreSQL"
if (-not (Test-Path $base)) { Fail "PostgreSQL not found under $base"; exit 1 }

$dirs = Get-ChildItem $base -Directory | Sort-Object Name -Descending
foreach ($d in $dirs) {
    Write-Host "    found: $($d.FullName)"
}

$psql = $null
foreach ($d in $dirs) {
    $cand = Join-Path $d.FullName "bin\psql.exe"
    if (Test-Path $cand) { $psql = $cand; break }
}
if (-not $psql) { Fail "psql.exe not located"; exit 1 }
Ok "Using $psql"

# --- 2. Get postgres superuser password ---
Step "Postgres superuser password"
$sec = Read-Host -AsSecureString "Enter the 'postgres' superuser password"
$pgpw = [System.Net.NetworkCredential]::new("", $sec).Password

# Test superuser connection
$env:PGPASSWORD = $pgpw
$superCheck = & $psql -U postgres -h localhost -d postgres -tAc "SELECT 1" 2>&1
if ($LASTEXITCODE -ne 0) {
    Fail "Could not connect as postgres. Output:"
    Write-Host $superCheck
    Remove-Item Env:PGPASSWORD
    exit 1
}
Ok "Connected as postgres"

# --- 3. Read expected password from appsettings.Development.json ---
Step "Reading expected password from appsettings.Development.json"
$devCfg = Join-Path $PSScriptRoot "src\KindHelp.Web\appsettings.Development.json"
if (-not (Test-Path $devCfg)) { Fail "Not found: $devCfg"; Remove-Item Env:PGPASSWORD; exit 1 }

$cfg = Get-Content $devCfg -Raw | ConvertFrom-Json
$connStr = $cfg.ConnectionStrings.DefaultConnection
if (-not $connStr) { Fail "No DefaultConnection in $devCfg"; Remove-Item Env:PGPASSWORD; exit 1 }

# Parse "Password=..." from the connection string
$kindhelpPw = $null
foreach ($pair in $connStr -split ';') {
    $kv = $pair -split '=', 2
    if ($kv.Count -eq 2 -and $kv[0].Trim().ToLower() -eq 'password') {
        $kindhelpPw = $kv[1].Trim()
    }
}
if (-not $kindhelpPw) { Fail "Could not extract Password= from connection string"; Remove-Item Env:PGPASSWORD; exit 1 }
Ok "Expected password from config: $kindhelpPw"

# --- 4. ALTER USER ---
Step "Resetting 'kindhelp' role password"
$escaped = $kindhelpPw -replace "'", "''"
& $psql -U postgres -h localhost -d postgres -c "ALTER USER kindhelp WITH PASSWORD '$escaped';" | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "ALTER USER failed"; Remove-Item Env:PGPASSWORD; exit 1 }
Ok "ALTER USER executed"

Remove-Item Env:PGPASSWORD

# --- 5. Verify with a real connection AS kindhelp ---
Step "Verifying - connecting as 'kindhelp' with the new password"
$env:PGPASSWORD = $kindhelpPw
# Use a single-quoted SQL literal so PowerShell 5.1 does not try to parse SQL operators.
$verify = & $psql -U kindhelp -h localhost -d kindhelp -tAc 'SELECT current_user' 2>&1
$exit = $LASTEXITCODE
Remove-Item Env:PGPASSWORD

if ($exit -eq 0) {
    $who = ($verify | Out-String).Trim()
    Ok "Connected. psql says: $who"
    Write-Host ""
    Write-Host "All good. Now run:" -ForegroundColor Cyan
    Write-Host "    .\run.ps1" -ForegroundColor White
} else {
    Fail "Verification failed. psql output:"
    Write-Host $verify
    Write-Host ""
    Write-Host "This means PostgreSQL is rejecting the kindhelp user even after the password reset." -ForegroundColor Yellow
    Write-Host "Likely culprits:" -ForegroundColor Yellow
    Write-Host "  - A different PostgreSQL service is running on port 5432." -ForegroundColor Yellow
    Write-Host "    Check with: netstat -ano | findstr 5432" -ForegroundColor Yellow
    Write-Host "  - pg_hba.conf rejects this user." -ForegroundColor Yellow
    Write-Host "    Look at the data\pg_hba.conf in your PostgreSQL install dir." -ForegroundColor Yellow
}
