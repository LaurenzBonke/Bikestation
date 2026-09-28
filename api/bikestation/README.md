# Smart Bikestation – Backend (ASP.NET Core)

REST-API für die Smart Bikestation. Sie nimmt die Sensordaten der ESP32 an, speichert sie
(aktuell SQLite) und stellt Slots, Messwerte und Meldungen für das Dashboard bereit.

## Starten

```powershell
cd bikestation
dotnet run
```

Die API läuft auf `http://localhost:5137`, die API-Doku (Scalar) auf `http://localhost:5137/scalar`.

## Ersten Admin anlegen

Passwörter stehen nie im Code. Ein Admin wird über die Kommandozeile angelegt:

```powershell
cd bikestation
dotnet run -- create-admin admin
```

Danach das Passwort eingeben (mind. 12 Zeichen). Gespeichert wird nur der Hash.

## Endpunkte

| Methode | Pfad | Auth | Zweck |
|---|---|---|---|
| POST | `/api/sensor-data` | – | ESP32 senden Messwerte |
| GET | `/api/slots` | – | Alle Stellplätze mit Status |
| GET | `/api/slots/{id}` | – | Ein Stellplatz |
| GET | `/api/slots/{id}/readings` | – | Messverlauf |
| GET | `/api/alerts` | – | Offene Meldungen |
| POST | `/api/alerts/{id}/resolve` | JWT | Meldung als erledigt markieren |
| POST | `/api/auth/login` | – | Login, liefert JWT (max. 5 Versuche/Minute) |
| GET | `/api/auth/me` | JWT | Token prüfen |

Geschützte Endpunkte erwarten den Header `Authorization: Bearer <token>`.

## Konfiguration

| Einstellung | Datei | Hinweis |
|---|---|---|
| `Occupancy:PressureThreshold` | `appsettings.json` | Ab diesem Druckwert gilt ein Platz als belegt. **Muss mit dem echten SEN0616 kalibriert werden.** |
| `Jwt:Key` | `appsettings.Development.json` | Nur ein Entwicklungs-Key. Auf dem Raspberry Pi per Umgebungsvariable `Jwt__Key` setzen (mind. 32 Zeichen, zufällig). |
| `Cors:AllowedOrigins` | `appsettings.json` | Erlaubte Frontend-Adressen |

## Datenbank

Aktuell SQLite (`bikestation.db`), wird beim Start automatisch angelegt.
Ändert sich das Datenmodell, die Datei `bikestation.db` löschen und die API neu starten
(danach Admin neu anlegen). Die endgültige Datenbank kommt später auf eine Proxmox-VM.
