# Server auf einem Windows-PC

So lief der Server beim Hackathon: API und Dashboard in einem Prozess auf Port 8080 auf einem Windows-11-PC im
lokalen Netz. Die Station (Raspberry Pi) und die Handys der Nutzer haben ihn über dessen IP-Adresse erreicht.

Voraussetzungen: .NET 10 SDK, Node.js 20.19+.

## Erstinstallation

```powershell
powershell -ExecutionPolicy Bypass -File deploy\publish.ps1    # baut alles nach %USERPROFILE%\Bikestation-Server
%USERPROFILE%\Bikestation-Server\create-admin.cmd admin         # einmalig
%USERPROFILE%\Bikestation-Server\start-server.cmd
```

`publish.ps1` legt beim ersten Mal `config.ps1` mit zufälligen Schlüsseln an (Produktionsmodus). Den Wert von
`Devices__ApiKey` aus `config.ps1` in `pi/config.ini` auf dem Pi eintragen. Für Zugriff von anderen Geräten
Port 8080 in der Windows-Firewall freigeben (als Administrator):

```powershell
New-NetFirewallRule -DisplayName "Bikestation 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow -Profile Any
```

## Server-Ordner

| Pfad | Inhalt |
|---|---|
| `app\` | laufende Version (`app-alt\` = vorherige, `app-neu\` = vorbereitete nächste) |
| `data\bikestation.db` | Datenbank (+ automatische Sicherungen vor Schema-Änderungen) |
| `config.ps1` | Port und **geheime Schlüssel** – nie weitergeben, nie committen |
| `logs\server-<Datum>.log` | Server-Log |

Der Server läuft nur, solange sein Fenster offen ist. Automatisch starten: Verknüpfung zu `start-server.cmd`
in `shell:startup` legen.

## Update

`deploy\update-server.cmd` → Rechtsklick → *Als Administrator ausführen*. Das Skript baut nach `app-neu`,
startet die neue Version probeweise gegen eine **Kopie** der Datenbank, tauscht dann `app` ↔ `app-neu`, startet
neu und prüft `/api/health`. Schlägt etwas fehl, läuft automatisch wieder die alte Version.

### Windows Smart App Control

Auf dem Hackathon-PC war Smart App Control aktiv. Es blockiert manchmal frisch gebaute, unsignierte DLLs
(„Eine Anwendungssteuerungsrichtlinie hat diese Datei blockiert“); das Update bricht dann mit
„Neue Version startet im Probelauf nicht“ ab, der laufende Server bleibt unberührt. Da Builds deterministisch
sind, hilft ein neuer Build desselben Codes nicht. Umgehung:

1. Mit wechselndem Versionsstempel selbst veröffentlichen, bis ein Build startet:
   `dotnet publish api/bikestation/bikestation/bikestation.csproj -c Release -o %USERPROFILE%\Bikestation-Server\app-neu -p:InformationalVersion=1.0.<Zeitstempel>`
2. `Frontend/api/dist` nach `app-neu\wwwroot` kopieren.
3. `deploy\update-server-ohne-build.cmd` als Administrator ausführen (gleiches Update, ohne neu zu bauen).

Smart App Control **nicht** ausschalten – unter Windows 11 lässt es sich danach nur durch Neuinstallation wieder
einschalten. Langfristig besser: den Server unter Linux betreiben (z. B. das Docker-Image aus dem Repo mit
eigenen Schlüsseln und `ASPNETCORE_ENVIRONMENT=Production`).
