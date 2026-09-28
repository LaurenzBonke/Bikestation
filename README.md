# Smart Bikestation

Hackathon 2026 – intelligente Fahrradparkstation mit 3 abschließbaren Boxen. Die Station erkennt freie und
belegte Plätze, zeigt sie live im Web-Dashboard, meldet mögliche Manipulation und erkennt mit KI
ungewöhnliches Verhalten. Keine Kameras, keine personenbezogenen Daten.

## Funktionen

- **Konto anlegen** (nur Benutzername + Passwort), **Box wählen und per Klick öffnen** – der Servo-Riegel öffnet
- Das Fahrrad wird per Ultraschall erkannt → die Box **verriegelt automatisch**; zum Abholen per Klick wieder öffnen
- **Alarm in der App**, wenn ein Fahrrad ohne Öffnen entfernt wird; die Box bleibt gesperrt, bis ein Admin sie freigibt
- Live-Übersicht, Statistik, Prognose, KI-Anomalieerkennung, Deutsch/Englisch/Niederländisch, Hell/Dunkel

Zustandsdiagramm der Box, ER-Diagramm und Abläufe: [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md)

## Architektur

```
3 Boxen (Ultraschall, Vibration, Servo-Riegel, grüne/rote LED)
   │  Raspberry-Pi-Agent (pi/) oder ESP32 (firmware/)
   │  WLAN, HTTP + JSON, Header X-Api-Key – Antwort enthält den Soll-Zustand des Riegels
   ▼
Server (Windows-PC, später Raspberry Pi 5 / Proxmox)
   ├── ASP.NET Core API (C#)      api/bikestation
   ├── Datenbank (SQLite, später Proxmox-VM)
   ├── KI-Anomalieerkennung (Python, Isolation Forest)   ai/
   └── React-Dashboard            Frontend/api
```

| Ordner | Inhalt | Anleitung |
|---|---|---|
| `api/bikestation` | C#-Backend (REST-API) | [README](api/bikestation/README.md) |
| `Frontend/api` | React-Dashboard | [README](Frontend/api/README.md) |
| `pi` | Raspberry-Pi-Agent: Sensoren, Servo-Riegel, Daten an die API | [README](pi/README.md) |
| `firmware` | ESP32-Firmware + Verdrahtung | [README](firmware/README.md) |
| `ai` | KI-Anomalieerkennung | [README](ai/README.md) |
| `tools/simulator.py` | Simuliert die 3 ESP32 – Testen ohne Hardware | siehe unten |
| `docs` | Architektur, ER-Diagramm, Abläufe, Sicherheit | [ARCHITEKTUR](docs/ARCHITEKTUR.md) |

## Alles lokal starten (ohne Hardware)

Vier Terminals, jeweils im Repo-Ordner:

```powershell
# 1. Backend (beim ersten Mal vorher: dotnet run -- create-admin admin)
cd api/bikestation/bikestation; dotnet run

# 2. Dashboard → http://localhost:5173
cd Frontend/api; npm install; npm run dev

# 3. Simulierte ESP32
python tools/simulator.py --api-key dev-geraete-key-nur-lokal

# 4. Optional: zusätzliche KI mit Isolation Forest (einmalig: pip install -r ai/requirements.txt)
#    Die statistische Anomalieerkennung läuft auch ohne sie direkt im Backend.
python ai/anomaly_service.py --api-key dev-geraete-key-nur-lokal
```

Für die Präsentation: Im Dashboard unter **Admin** anmelden und **Demo-Daten erzeugen** klicken –
dann zeigen Statistik und KI sofort Ergebnisse. Eine Manipulation vorführen:
`python tools/simulator.py --api-key dev-geraete-key-nur-lokal --tamper-slot 2`

## Als Webserver betreiben (Windows-PC)

API und Dashboard laufen zusammen in einem Prozess auf Port 8080 (das gebaute Dashboard liegt in `wwwroot`).

```powershell
powershell -ExecutionPolicy Bypass -File deploy\publish.ps1   # baut alles nach %USERPROFILE%\Bikestation-Server
%USERPROFILE%\Bikestation-Server\create-admin.cmd admin      # einmalig
%USERPROFILE%\Bikestation-Server\start-server.cmd
```

`publish.ps1` erzeugt beim ersten Mal `config.ps1` mit zufälligen Schlüsseln (Produktionsmodus).
Datenbank (`data\`), Logs und `config.ps1` bleiben bei Updates erhalten. Für Zugriff von anderen
Geräten muss Port 8080 in der Windows-Firewall freigegeben werden.

**Laufenden Server aktualisieren:** `deploy\update-server.cmd` (Rechtsklick → Als Administrator ausführen).
Baut die neue Version, macht einen Probestart gegen eine Kopie der Datenbank, tauscht dann aus und fällt bei
einem Fehler automatisch auf die alte Version zurück. Die Datenbank wird dabei ohne Datenverlust umgestellt.

## Sicherheit

- ESP32 und KI-Dienst authentifizieren sich mit API-Key, Admins mit JWT (60 min gültig)
- Passwörter nur als Hash, Admin-Anlage nur per Kommandozeile
- Rate Limiting am Login, Eingabevalidierung an allen Endpunkten
- Schlüssel stehen nicht im Repo (nur Entwicklungswerte) – auf dem Pi per Umgebungsvariable
- Keine personenbezogenen Daten: gespeichert werden nur Stellplatz, Sensorwerte und Zeit
