# Aktualisiert den laufenden Bikestation-Server sicher auf den Stand dieses Repos.
#
#  1. baut Frontend und Backend in "app-neu"
#  2. Probestart der neuen Version gegen eine KOPIE der Datenbank (Live-Server bleibt unberührt)
#  3. stoppt den Live-Server, tauscht app <-> app-neu (alte Version bleibt als "app-alt")
#  4. startet neu und prüft /api/health – schlägt das fehl, wird automatisch die alte Version gestartet
#
# Die Datenbank wird beim ersten Start der neuen Version automatisch umgestellt (Sicherung liegt in data\).
# Aufruf (als Administrator, falls der Server als Administrator läuft):
#   powershell -ExecutionPolicy Bypass -File deploy\update-server.ps1

param(
    [string]$Target = "$env:USERPROFILE\Bikestation-Server",
    [switch]$SkipBuild,
    [int]$TrialPort = 8099
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$app = Join-Path $Target "app"
$new = Join-Path $Target "app-neu"
$old = Join-Path $Target "app-alt"
$db = Join-Path $Target "data\bikestation.db"

function Step($text) { Write-Host "== $text" -ForegroundColor Cyan }

function Wait-Healthy([string]$url, [int]$seconds = 25) {
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) {
        try { if ((Invoke-WebRequest "$url/api/health" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200) { return $true } } catch {}
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Stop-Server([int]$port) {
    # Prozess, der den Port belegt (dotnet), und die Start-Fenster (halten den Ordner "app" fest)
    Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
    Get-CimInstance Win32_Process |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$Target*start-server*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Server auf Port $port lässt sich nicht beenden – Skript als Administrator ausführen."
    }
}

function Move-WithRetry([string]$from, [string]$to) {
    for ($i = 0; $i -lt 20; $i++) {
        try { Rename-Item $from (Split-Path $to -Leaf) -ErrorAction Stop; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "Ordner $from ist noch in Benutzung."
}

function Start-Server {
    Start-Process -FilePath (Join-Path $Target "start-server.cmd") -WorkingDirectory $Target -WindowStyle Minimized
}

. (Join-Path $Target "config.ps1")
$livePort = $Port
$liveUrl = "http://localhost:$livePort"

# ---------------------------------------------------------------- 1. Bauen
if (-not $SkipBuild) {
    Step "Neue Version bauen"
    if (Test-Path $new) { Remove-Item $new -Recurse -Force }
    Push-Location (Join-Path $repo "Frontend\api")
    npm install --no-fund --no-audit | Out-Null
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend-Build fehlgeschlagen" }
    Pop-Location
    dotnet publish (Join-Path $repo "api\bikestation\bikestation\bikestation.csproj") -c Release -o $new --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen" }
    Copy-Item (Join-Path $repo "Frontend\api\dist") (Join-Path $new "wwwroot") -Recurse -Force
    Copy-Item (Join-Path $PSScriptRoot "update-server.ps1") $Target -Force
}
if (-not (Test-Path (Join-Path $new "bikestation.dll"))) { throw "Keine neue Version in $new gefunden." }

# ---------------------------------------------------------------- 2. Probestart gegen Kopie der Datenbank
Step "Probestart der neuen Version auf Port $TrialPort (mit Kopie der Datenbank)"
$trialDb = Join-Path $env:TEMP "bikestation-probe.db"
Remove-Item "$trialDb*" -Force -ErrorAction SilentlyContinue
if (Test-Path $db) {
    foreach ($suffix in "", "-wal", "-shm") { if (Test-Path "$db$suffix") { Copy-Item "$db$suffix" "$trialDb$suffix" } }
}
$savedUrls = $env:ASPNETCORE_URLS; $savedDb = $env:ConnectionStrings__Bikestation
$env:ASPNETCORE_URLS = "http://localhost:$TrialPort"
$env:ConnectionStrings__Bikestation = "Data Source=$trialDb"
$trial = Start-Process dotnet -ArgumentList "bikestation.dll" -WorkingDirectory $new -PassThru -WindowStyle Hidden
$trialOk = Wait-Healthy "http://localhost:$TrialPort"
Stop-Process -Id $trial.Id -Force -ErrorAction SilentlyContinue
$env:ASPNETCORE_URLS = $savedUrls; $env:ConnectionStrings__Bikestation = $savedDb
Remove-Item "$trialDb*" -Force -ErrorAction SilentlyContinue
if (-not $trialOk) {
    throw "Neue Version startet im Probelauf nicht (evtl. blockiert Smart App Control). Live-Server wurde NICHT verändert."
}
Write-Host "   Probestart erfolgreich" -ForegroundColor Green

# ---------------------------------------------------------------- 3. Austauschen
Step "Live-Server stoppen und Versionen tauschen"
Stop-Server $livePort
if (Test-Path $old) { Remove-Item $old -Recurse -Force }
Move-WithRetry $app $old
Move-WithRetry $new $app

# ---------------------------------------------------------------- 4. Starten und prüfen
Step "Neue Version starten"
Start-Server
if (Wait-Healthy $liveUrl 40) {
    Write-Host "FERTIG: Server läuft mit der neuen Version ($liveUrl). Alte Version liegt in app-alt." -ForegroundColor Green
    exit 0
}

Write-Host "!! Neue Version antwortet nicht – starte die alte Version" -ForegroundColor Red
Stop-Server $livePort
Move-WithRetry $app $new
Move-WithRetry $old $app
Start-Server
if (Wait-Healthy $liveUrl 40) { Write-Host "Alte Version läuft wieder." -ForegroundColor Yellow } else { Write-Host "!! Auch die alte Version startet nicht – Logs prüfen: $Target\logs" -ForegroundColor Red }
exit 1
