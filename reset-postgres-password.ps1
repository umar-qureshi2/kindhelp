# Reset the local PostgreSQL 'postgres' superuser password.
# Run from an ELEVATED PowerShell:
#   .\reset-postgres-password.ps1 -NewPassword 1010
#
# What it does, safely:
#   1. Backs up pg_hba.conf.
#   2. Temporarily switches local connections to 'trust' (no password needed).
#   3. Restarts the PostgreSQL service.
#   4. Connects with no password and runs ALTER USER to set the new password.
#   5. Restores pg_hba.conf and restarts the service again.
# The 'trust' window only exists while this script is running, on this machine.

param(
    [Parameter(Mandatory = $true)][string]$NewPassword
)

$ErrorActionPreference = "Stop"

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "  ok  $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "  !!  $msg" -ForegroundColor Red }

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Fail "Must run as Administrator."
    exit 1
}

# Locate the latest PostgreSQL install.
$base = "C:\Program Files\PostgreSQL"
if (-not (Test-Path $base)) { Fail "PostgreSQL not found under $base."; exit 1 }

$pgDir = Get-ChildItem $base -Directory | Sort-Object Name -Descending | Select-Object -First 1
$psql  = Join-Path $pgDir.FullName "bin\psql.exe"
$data  = Join-Path $pgDir.FullName "data"
$hba   = Join-Path $data "pg_hba.conf"
$bak   = Join-Path $data "pg_hba.conf.kindhelp-bak"

if (-not (Test-Path $hba))  { Fail "pg_hba.conf not found at $hba"; exit 1 }
if (-not (Test-Path $psql)) { Fail "psql.exe not found at $psql";  exit 1 }

$svc = Get-Service -Name "postgresql-*" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $svc) { Fail "No PostgreSQL service found."; exit 1 }

Step "Backing up pg_hba.conf"
Copy-Item $hba $bak -Force
Ok "Backup at $bak"

$restored = $false
try {
    Step "Switching local auth to 'trust' temporarily"
    $lines = Get-Content $hba
    $patched = foreach ($line in $lines) {
        # Match a typical hba line and replace the trailing auth method with 'trust'.
        if ($line -match '^\s*(local|host|hostssl|hostnossl)\s+\S+\s+\S+\s+(?:\S+\s+)?(scram-sha-256|md5|password|peer|ident)\s*$') {
            $line -replace '(scram-sha-256|md5|password|peer|ident)\s*$', 'trust'
        } else {
            $line
        }
    }
    # Write BOM-less UTF-8 — PostgreSQL refuses to parse pg_hba.conf if it has a BOM.
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($hba, ($patched -join "`r`n") + "`r`n", $utf8NoBom)
    Ok "pg_hba.conf patched (BOM-less)"

    Step "Restarting $($svc.Name)"
    Restart-Service $svc.Name -Force
    Start-Sleep -Seconds 3
    Ok "Service restarted"

    Step "Setting new password for role 'postgres'"
    # Escape single quotes in the password for the SQL literal.
    $escaped = $NewPassword -replace "'", "''"
    & $psql -U postgres -h localhost -d postgres -c "ALTER USER postgres WITH PASSWORD '$escaped';"
    if ($LASTEXITCODE -ne 0) { throw "ALTER USER failed (exit $LASTEXITCODE)" }
    Ok "Password updated"
}
finally {
    Step "Restoring pg_hba.conf"
    Copy-Item $bak $hba -Force
    Remove-Item $bak -ErrorAction SilentlyContinue
    $restored = $true
    Ok "Original pg_hba.conf restored"

    Step "Restarting $($svc.Name)"
    Restart-Service $svc.Name -Force
    Start-Sleep -Seconds 3
    Ok "Service restarted"
}

Write-Host ""
Write-Host "Done. Now re-run:" -ForegroundColor Cyan
Write-Host "    .\setup.ps1" -ForegroundColor White
Write-Host "and when it asks for the postgres password, enter: $NewPassword" -ForegroundColor Gray
