# Baut Frontend und Backend und legt beides in den Server-Ordner.
# Die Datenbank (data\) und die Schlüssel (config.ps1) im Server-Ordner bleiben dabei erhalten.
#
# Aufruf aus dem Repo-Ordner:
#   powershell -ExecutionPolicy Bypass -File deploy\publish.ps1
#   powershell -ExecutionPolicy Bypass -File deploy\publish.ps1 -Target D:\Bikestation-Server

param(
    [string]$Target = "$env:USERPROFILE\Bikestation-Server"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$app = Join-Path $Target "app"

Write-Host "== Frontend bauen" -ForegroundColor Cyan
Push-Location (Join-Path $repo "Frontend\api")
npm install --no-fund --no-audit
if ($LASTEXITCODE -ne 0) { throw "npm install fehlgeschlagen" }
npm run build
if ($LASTEXITCODE -ne 0) { throw "Frontend-Build fehlgeschlagen" }
Pop-Location

Write-Host "== Backend veröffentlichen nach $app" -ForegroundColor Cyan
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
dotnet publish (Join-Path $repo "api\bikestation\bikestation\bikestation.csproj") -c Release -o $app --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen" }

Write-Host "== Frontend in wwwroot kopieren" -ForegroundColor Cyan
Copy-Item (Join-Path $repo "Frontend\api\dist") (Join-Path $app "wwwroot") -Recurse -Force

# Start-Skripte kopieren
Copy-Item (Join-Path $PSScriptRoot "start-server.ps1") $Target -Force
Copy-Item (Join-Path $PSScriptRoot "start-server.cmd") $Target -Force
Copy-Item (Join-Path $PSScriptRoot "create-admin.cmd") $Target -Force

# Beim ersten Mal: Konfiguration mit zufälligen Schlüsseln anlegen
$config = Join-Path $Target "config.ps1"
if (-not (Test-Path $config)) {
    Write-Host "== Neue Konfiguration mit zufälligen Schlüsseln: $config" -ForegroundColor Cyan
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $jwtBytes = New-Object byte[] 48; $rng.GetBytes($jwtBytes)
    $apiBytes = New-Object byte[] 24; $rng.GetBytes($apiBytes)
    $jwtKey = [Convert]::ToBase64String($jwtBytes)
    $apiKey = ($apiBytes | ForEach-Object { $_.ToString("x2") }) -join ""
    @"
# Server-Konfiguration – enthält geheime Schlüssel, nicht weitergeben und nicht committen.
`$Port = 8080
`$env:ASPNETCORE_ENVIRONMENT = "Production"
`$env:ASPNETCORE_URLS = "http://0.0.0.0:`$Port"
`$env:ConnectionStrings__Bikestation = "Data Source=$Target\data\bikestation.db"
`$env:Jwt__Key = "$jwtKey"
`$env:Devices__ApiKey = "$apiKey"
"@ | Set-Content $config -Encoding UTF8
}

Write-Host "Fertig. Starten mit: $Target\start-server.cmd" -ForegroundColor Green
