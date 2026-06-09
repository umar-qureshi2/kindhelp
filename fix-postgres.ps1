# Recovers PostgreSQL after a failed pg_hba.conf edit.
# Run from an ELEVATED PowerShell:
#   .\fix-postgres.ps1
#
# Restores pg_hba.conf from our backup (if present), starts the service,
# and prints the latest log tail if the service still refuses to start.

$ErrorActionPreference = "Stop"

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  ok  $m" -ForegroundColor Green }
function Fail($m) { Write-Host "  !!  $m" -ForegroundColor Red }

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Fail "Must run as Administrator."
    exit 1
}

$base = "C:\Program Files\PostgreSQL"
if (-not (Test-Path $base)) { Fail "PostgreSQL not found under $base."; exit 1 }
$pgDir = Get-ChildItem $base -Directory | Sort-Object Name -Descending | Select-Object -First 1
$data  = Join-Path $pgDir.FullName "data"
$hba   = Join-Path $data "pg_hba.conf"
$bak   = Join-Path $data "pg_hba.conf.kindhelp-bak"

$svc = Get-Service -Name "postgresql-*" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $svc) { Fail "No PostgreSQL service installed."; exit 1 }

Step "Stopping $($svc.Name) if running"
if ($svc.Status -ne "Stopped") {
    Stop-Service $svc.Name -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}
Ok "Stopped"

if (Test-Path $bak) {
    Step "Restoring pg_hba.conf from backup"
    Copy-Item $bak $hba -Force
    Remove-Item $bak -ErrorAction SilentlyContinue
    Ok "Restored"
} else {
    Ok "No leftover backup found; pg_hba.conf left as-is"
}

# Strip BOM from pg_hba.conf just in case the previous run wrote one.
$bytes = [System.IO.File]::ReadAllBytes($hba)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Step "Removing UTF-8 BOM from pg_hba.conf"
    [System.IO.File]::WriteAllBytes($hba, $bytes[3..($bytes.Length - 1)])
    Ok "BOM removed"
}

Step "Starting $($svc.Name)"
$started = $false
try {
    Start-Service $svc.Name
    Start-Sleep -Seconds 3
    $svc.Refresh()
    if ($svc.Status -eq "Running") {
        $started = $true
    }
} catch {
    # fall through to log dump below
}

if ($started) {
    Ok "Service is running"
    Write-Host ""
    Write-Host "PostgreSQL is healthy again." -ForegroundColor Cyan
    Write-Host "Now run the fixed password reset:" -ForegroundColor Cyan
    Write-Host "    .\reset-postgres-password.ps1 -NewPassword 1010" -ForegroundColor White
    exit 0
}

Fail "Service still won't start. Dumping latest log:"
$logDir = Join-Path $data "log"
if (Test-Path $logDir) {
    $latest = Get-ChildItem $logDir -Filter "*.log" -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Host ""
        Write-Host "=== Last 40 lines of $($latest.FullName) ===" -ForegroundColor Yellow
        Get-Content $latest.FullName -Tail 40
        Write-Host "==="
    } else {
        Write-Host "No log files found under $logDir"
    }
} else {
    Write-Host "Log directory not found at $logDir"
}
exit 1
