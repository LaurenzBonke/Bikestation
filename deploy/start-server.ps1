# Startet den Bikestation-Server (API + Dashboard) mit der Konfiguration aus config.ps1.
# Log-Ausgaben erscheinen im Fenster und in logs\server-<Datum>.log.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "config.ps1")

$logs = Join-Path $PSScriptRoot "logs"
New-Item -ItemType Directory -Force $logs | Out-Null
$log = Join-Path $logs ("server-{0:yyyy-MM-dd}.log" -f (Get-Date))

Write-Host "Bikestation-Server startet auf Port $Port ..." -ForegroundColor Cyan
Write-Host "Dashboard: http://localhost:$Port   (Beenden: Strg+C)" -ForegroundColor Cyan

Set-Location (Join-Path $PSScriptRoot "app")
# Nicht bei stderr-Ausgaben abbrechen (PowerShell 5.1 behandelt sie sonst als Fehler)
$ErrorActionPreference = "Continue"
dotnet bikestation.dll 2>&1 | Tee-Object -FilePath $log -Append
