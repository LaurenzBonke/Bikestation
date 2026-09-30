# Smart Bikestation – Backend (ASP.NET Core)

REST-API der Smart Bikestation. Sie nimmt die Messwerte der Station (Raspberry Pi) an, entscheidet über
die Zustandsmaschine, wann eine Box öffnet, verriegelt oder Alarm schlägt, speichert alles in SQLite und
liefert das gebaute Dashboard aus `wwwroot` mit aus.

## Starten

```powershell
cd bikestation
dotnet run                             # Entwicklung, nur vom eigenen Rechner erreichbar (localhost:5137)
dotnet run --launch-profile http-lan   # auch für den Raspberry Pi im LAN erreichbar (0.0.0.0:5137)
dotnet run --launch-profile demo       # Demo-Modus: virtuelle Station, Demo-Konten, Beispieldaten
```

API-Doku (Scalar, nur Development): `http://localhost:5137/scalar`. Testanfragen: `bikestation.http`.

## Ersten Admin anlegen

Passwörter stehen nie im Code. Ein Admin wird über die Kommandozeile angelegt:

```powershell
cd bikestation
dotnet run -- create-admin admin
```

Danach das Passwort eingeben (mind. 12 Zeichen). Gespeichert wird nur der Hash.

## Demo-Modus

Mit `ASPNETCORE_ENVIRONMENT=Demo` (Launch-Profil `demo`, `demo.py` oder Docker) gilt zusätzlich
`appsettings.Demo.json`:

- **Virtuelle Station** (`Services/VirtualStation.cs`): ersetzt den Raspberry Pi. Sie meldet jede Sekunde für
  jede Box einen Abstand über denselben `SensorDataService` wie die echte Station und merkt sich die Antwort
  (Riegel, LED). Ob ein Fahrrad in der Box steht, stellt man im Dashboard ein.
- **Demo-Konten** `demo` / `bikestation-demo` und `admin` / `bikestation-admin`, beim Start angelegt.
- **Beispieldaten** der letzten 14 Tage, wenn die Datenbank noch keine Messwerte hat.
- Eigene Datenbankdatei `bikestation-demo.db` und Schlüssel, die nur für die Demo gedacht sind.

## Endpunkte

| Methode | Pfad | Schutz | Zweck |
|---|---|---|---|
| POST | `/api/sensor-data` | API-Key | Station sendet einen Messwert, bekommt `{boxState, lockOpen}` zurück |
| GET | `/api/device/boxes` | API-Key | Soll-Zustand aller Riegel (für den Pi) |
| GET | `/api/slots`, `/api/slots/{id}` | – | Stationen mit Status, Ort, online/offline |
| GET | `/api/slots/{id}/readings` | – | Messverlauf einer Station |
| GET | `/api/sensor-readings?afterId=` | – | Messwerte aller Stationen (für externe Auswertungen) |
| GET | `/api/boxes` | – (mit JWT zusätzlich `isMine`) | Alle Boxen mit Zustand und Ort |
| POST | `/api/boxes/{id}/book` | JWT | Freie Box buchen, Riegel öffnet |
| POST | `/api/boxes/{id}/cancel` | JWT (Besitzer) | Buchung abbrechen, solange kein Fahrrad drin ist |
| POST | `/api/boxes/{id}/pickup` | JWT (Besitzer) | Verriegelte Box zum Abholen öffnen |
| POST | `/api/boxes/{id}/release` | JWT (Admin) | Nach Alarm gesperrte Box freigeben |
| POST · PUT · DELETE | `/api/boxes`, `/api/boxes/{id}` | JWT (Admin) | Station anlegen, Ort ändern, freie Station löschen |
| GET | `/api/boxes/occupancy` | JWT (Admin) | Wer steht an welcher Box |
| GET | `/api/boxes/events?limit=&slotId=` | JWT (Admin) | Box-Protokoll |
| GET | `/api/me` | JWT | Eigene Box, eigene Meldungen, Verlauf |
| POST | `/api/me/alerts/{id}/ack` | JWT | Eigene Meldung als gesehen markieren |
| GET | `/api/alerts` | JWT (Admin) oder API-Key | Offene Meldungen |
| POST | `/api/alerts/{id}/resolve` | JWT (Admin) | Meldung als erledigt markieren |
| POST | `/api/anomalies` | API-Key | Externer Dienst meldet eine Anomalie |
| POST | `/api/auth/register` | – | Nutzerkonto anlegen (nur Benutzername + Passwort), liefert JWT |
| POST | `/api/auth/login` | – | Login, liefert JWT (max. 5 Versuche/Minute) |
| GET | `/api/auth/me` | JWT | Token prüfen |
| GET | `/api/statistics?days=7` | – | Auslastung nach Uhrzeit, pro Tag, je Station, Ereignisse |
| GET | `/api/forecast?hours=6` | – | Auslastungsprognose der nächsten Stunden |
| GET | `/api/health` | – | Läuft die API, ist die Datenbank erreichbar? |
| GET | `/api/demo` | – | Demo-Modus aktiv? Demo-Konten |
| GET | `/api/demo/station` | – (nur Demo) | Virtuelle Station: Riegel, LED, Abstand je Box |
| POST | `/api/demo/station/{id}/bike` | – (nur Demo) | Virtuelle Station: Fahrrad hineinstellen/herausnehmen `{present}` |
| POST | `/api/demo/generate?days=7` | JWT (Admin) | Beispieldaten erzeugen (Development oder `Demo:Enabled`) |

- **API-Key:** Header `X-Api-Key: <Devices:ApiKey>` – für die Station
- **JWT:** Header `Authorization: Bearer <token>` – Nutzer 12 h, Admins 60 min

## Konfiguration

| Einstellung | Standard | Hinweis |
|---|---|---|
| `Box:StationCount` / `Box:Locations` | 1 / `["Hackathon Halle"]` | Nur beim allerersten Start (leere DB), danach verwalten Admins die Stationen |
| `Box:BikePresentMaxDistanceCm` | 7 | Fahrrad gilt als da, wenn der Ultraschall höchstens so nah misst |
| `Box:ParkConfirmSeconds` / `LeaveConfirmSeconds` | 5 | So lange muss das Rad da bzw. weg sein, bevor der Riegel schaltet |
| `Box:AlarmConfirmSeconds` | 3 | So lange fehlt das Rad in einer verriegelten Box, bevor Alarm ausgelöst wird |
| `Box:OpenForParkingTimeoutSeconds` / `OpenForPickupTimeoutSeconds` | 120 | Zeitlimit für offene Boxen |
| `Devices:ApiKey` | – | Schlüssel für die Station (mind. 16 Zeichen) |
| `Devices:OfflineAfterSeconds` | 30 | Ohne Meldung so lange → Station „Offline“, Buchen gesperrt |
| `Jwt:Key` | – | Signaturschlüssel für JWT (mind. 32 Zeichen) |
| `Anomaly:Enabled` / `Anomaly:ZThreshold` | true / 6 | Eingebaute statistische Anomalieerkennung (robuster Z-Score) |
| `Occupancy:PressureThreshold` | 500 | Nur für einen optionalen Drucksensor – beim Hackathon nicht angeschlossen |
| `Retention:ReadingDays` | 90 | Messwerte älter als das werden täglich gelöscht (0 = nie) |
| `Demo:Enabled` | false | Beispieldaten-Button auch außerhalb von Development erlauben |
| `Demo:VirtualStation` / `HistoryDays` / `Accounts` | aus | Demo-Modus, siehe oben (nur in `appsettings.Demo.json` gesetzt) |
| `Cors:AllowedOrigins` | localhost:5173 | Erlaubte Frontend-Adressen |

`appsettings.Development.json` und `appsettings.Demo.json` enthalten nur Schlüssel für Entwicklung und Demo.
**Im Betrieb** eigene, zufällige Werte als Umgebungsvariablen setzen – ohne sie startet die API absichtlich nicht
(auf dem Windows-Server erledigt das `deploy/publish.ps1`):

```bash
export Jwt__Key="$(openssl rand -base64 48)"
export Devices__ApiKey="$(openssl rand -hex 24)"
```

## Datenbank

SQLite (`bikestation.db`), wird beim Start automatisch angelegt. Passt eine vorhandene Datei nicht mehr zum
Datenmodell (z. B. nach einem Update), wird sie gesichert (`bikestation.db.vor-update-<Zeitpunkt>`) und alle
Daten werden spaltenweise in ein frisches Schema übernommen (`Data/DatabaseInitializer.cs`). Für einen echten
Datenbankserver wäre der nächste Schritt EF-Core-Migrations.

## Tests

```powershell
dotnet test      # 67 Tests, ca. 15 s
```

Integrationstests (echte API + eigene SQLite-Datei pro Test) für Konten, den kompletten Box-Ablauf, Alarm,
Rechte, Zeitlimits, Offline-Erkennung, Statistik, Prognose, Datenbank-Update, Stationsverwaltung und den
Demo-Modus sowie Unit-Tests der Anomalieerkennung liegen in `bikestation.Tests`.
Architektur, ER-Diagramm und Abläufe: [docs/ARCHITEKTUR.md](../../docs/ARCHITEKTUR.md).
