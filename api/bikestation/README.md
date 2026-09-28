# Smart Bikestation – Backend (ASP.NET Core)

REST-API für die Smart Bikestation. Sie nimmt die Sensordaten der ESP32 an, speichert sie
(aktuell SQLite) und stellt Slots, Messwerte, Statistik und Meldungen für das Dashboard bereit.

## Starten

```powershell
cd bikestation
dotnet run                          # nur vom eigenen Rechner erreichbar (localhost:5137)
dotnet run --launch-profile http-lan  # auch für ESP32 im WLAN erreichbar (0.0.0.0:5137)
```

API-Doku (Scalar): `http://localhost:5137/scalar`. Testanfragen: `bikestation.http` in Visual Studio.

## Ersten Admin anlegen

Passwörter stehen nie im Code. Ein Admin wird über die Kommandozeile angelegt:

```powershell
cd bikestation
dotnet run -- create-admin admin
```

Danach das Passwort eingeben (mind. 12 Zeichen). Gespeichert wird nur der Hash.

## Endpunkte

| Methode | Pfad | Schutz | Zweck |
|---|---|---|---|
| POST | `/api/sensor-data` | API-Key | ESP32 senden Messwerte |
| GET | `/api/slots` | – | Alle Stellplätze mit Status, online/offline, Warnungen |
| GET | `/api/slots/{id}` | – | Ein Stellplatz |
| GET | `/api/slots/{id}/readings` | – | Messverlauf eines Platzes |
| GET | `/api/sensor-readings?afterId=` | – | Messwerte aller Plätze (für den KI-Dienst) |
| GET | `/api/statistics?days=7` | – | Auslastung nach Uhrzeit, je Platz, Ereignisse |
| GET | `/api/alerts` | – | Offene Meldungen |
| POST | `/api/alerts/{id}/resolve` | JWT | Meldung als erledigt markieren |
| POST | `/api/anomalies` | API-Key | KI-Dienst meldet Anomalie |
| POST | `/api/auth/login` | – | Login, liefert JWT (max. 5 Versuche/Minute) |
| GET | `/api/auth/me` | JWT | Token prüfen |
| POST | `/api/demo/generate?days=7` | JWT | Demo-Daten erzeugen (nur Development) |

- **API-Key:** Header `X-Api-Key: <Devices:ApiKey>` – für Geräte (ESP32, KI-Dienst)
- **JWT:** Header `Authorization: Bearer <token>` – für Admins im Dashboard

## Konfiguration

| Einstellung | Standard | Hinweis |
|---|---|---|
| `Occupancy:PressureThreshold` | 500 | Ab diesem Druckwert gilt ein Platz als belegt. **Mit dem echten SEN0616 kalibrieren.** |
| `Occupancy:TamperAlertCooldownSeconds` | 60 | Mindestabstand zwischen zwei Vibrations-Meldungen pro Platz |
| `Occupancy:AnomalyAlertCooldownSeconds` | 300 | Dasselbe für KI-Anomalien |
| `Devices:ApiKey` | – | Schlüssel für ESP32 und KI-Dienst (mind. 16 Zeichen) |
| `Devices:OfflineAfterSeconds` | 30 | Ohne Meldung so lange → Platz „Offline“ |
| `Jwt:Key` | – | Signaturschlüssel für JWT (mind. 32 Zeichen) |
| `Cors:AllowedOrigins` | localhost:5173 | Erlaubte Frontend-Adressen |

`appsettings.Development.json` enthält nur Entwicklungs-Schlüssel. **Auf dem Raspberry Pi** eigene,
zufällige Werte als Umgebungsvariablen setzen – ohne sie startet die API absichtlich nicht:

```bash
export Jwt__Key="$(openssl rand -base64 48)"
export Devices__ApiKey="$(openssl rand -hex 24)"
```

## Datenbank

Aktuell SQLite (`bikestation.db`), wird beim Start automatisch angelegt.
**Ändert sich das Datenmodell, die Datei `bikestation.db` löschen** und die API neu starten
(danach Admin neu anlegen). Die endgültige Datenbank kommt später auf eine Proxmox-VM –
dann auf EF-Core-Migrations umstellen.
