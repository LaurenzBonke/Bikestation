# Smart Bikestation – Architektur

Die Diagramme sind in Mermaid geschrieben und werden auf GitHub direkt als Grafik angezeigt.

## Idee

Die Station hat abschließbare **Boxen** (Prototyp: 3). Nutzer legen ein Konto an, wählen im Web-Dashboard eine
freie Box und öffnen sie per Klick. Ein Servo-Riegel öffnet, das Fahrrad wird eingestellt, der Ultraschallsensor
erkennt es, und die Box verriegelt automatisch. Zum Abholen öffnet der Nutzer die Box wieder per Klick. Wird ein
Fahrrad entfernt, **ohne** dass die Box geöffnet wurde, bekommt der Nutzer sofort einen Alarm in der App.

Das erfüllt die Grundaufgabe: freie Plätze erkennen und anzeigen, Manipulation erkennen (Vibration,
KI-Anomalieerkennung, unerwartete Entnahme), Daten speichern und auswerten (Statistik, Prognose), keine
personenbezogenen Daten (Konto = nur Benutzername und Passwort-Hash), barrierearm, in 3 Sprachen.

## Systemübersicht

```mermaid
flowchart LR
    subgraph Box["Box 1–3"]
        U[Ultraschall<br/>Grove V2.0] --> D
        V[Vibration] --> D
        D{{Raspberry Pi<br/>bikestation_agent.py<br/>oder ESP32}} --> S[Servo-Riegel]
        D --> L[LED grün/rot]
    end

    D -- "HTTP POST /api/sensor-data<br/>X-Api-Key<br/>Antwort: boxState, lockOpen" --> API

    subgraph Server["Server (Windows-PC, später Raspberry Pi / Proxmox)"]
        API[ASP.NET Core API<br/>Port 8080]
        SM[Box-Zustandsmaschine<br/>BoxService + BoxWorker]
        AN[Anomalieerkennung<br/>Hintergrunddienst]
        DB[(SQLite)]
        UI[React-Dashboard<br/>wwwroot]
        API --- SM --- DB
        AN --- DB
        API --> UI
    end

    N[Nutzer<br/>Handy/Browser] -- "JWT: Box buchen, öffnen, abholen" --> API
    A[Admin] -- "JWT (Rolle Admin): Meldungen, Boxen freigeben" --> API
```

## Zustände einer Box

```mermaid
stateDiagram-v2
    [*] --> Free
    Free --> OpenForParking: Nutzer bucht (Riegel öffnet)
    OpenForParking --> Locked: Fahrrad ≤ 5 cm, 5 s lang erkannt (Riegel schließt)
    OpenForParking --> Free: Abbrechen oder 2 min kein Fahrrad
    Locked --> OpenForPickup: Besitzer klickt "Abholen" (Riegel öffnet)
    OpenForPickup --> Free: Fahrrad 5 s weg (Riegel schließt)
    OpenForPickup --> Locked: 2 min nicht entnommen
    Locked --> Blocked: Fahrrad weg OHNE Öffnen → ALARM an Nutzer und Admin
    Blocked --> Free: Admin prüft vor Ort und gibt frei
```

Abstände und Zeiten stehen in `appsettings.json` unter `Box` (kalibrierbar). Wartezeiten zählen erst ab dem
Zustandswechsel, damit einzelne Messausreißer den Riegel nicht schalten. Buchen und Abholen sind nur möglich,
wenn die Station verbunden ist.

## Datenmodell (ER-Diagramm)

```mermaid
erDiagram
    Users ||--o{ Parkings : "hat"
    Slots ||--o{ Parkings : "hat"
    Slots ||--o{ SensorReadings : "hat"
    Slots ||--o{ Alerts : "hat"
    Users |o--o{ Alerts : "betrifft"

    Users {
        int Id PK
        string Username "eindeutig, 3–32 Zeichen"
        string PasswordHash "PBKDF2"
        string Role "User | Admin"
        datetime CreatedAt
    }
    Slots {
        int Id PK
        string Name
        string Status "Sensor: Unknown | Free | Occupied"
        datetime LastUpdated "letzte Meldung (online/offline)"
        string BoxState "Free | OpenForParking | Locked | OpenForPickup | Blocked"
        datetime BoxStateChangedAt
        datetime BikePresentSince "entprellt die Erkennung"
        datetime BikeAbsentSince
        int ActiveParkingId "laufender Parkvorgang"
    }
    Parkings {
        int Id PK
        int SlotId FK
        int UserId FK
        datetime BookedAt
        datetime ParkedAt
        datetime PickupRequestedAt
        datetime EndedAt
        string EndReason "Completed | Cancelled | TimedOut | BikeRemoved"
    }
    SensorReadings {
        int Id PK
        int SlotId FK
        int Pressure "ADC 0–4095"
        int Distance "cm"
        bool Vibration
        bool Occupied "vom Backend berechnet"
        datetime Timestamp "UTC, vom Server gesetzt"
    }
    Alerts {
        int Id PK
        int SlotId FK
        int UserId "Besitzer der Box, sonst leer"
        string Type "PossibleTampering | SensorMismatch | Anomaly | BikeRemoved"
        string Severity "Info | Warning | Critical"
        string Message
        double Score "nur KI, 0–1"
        datetime Timestamp
        bool Resolved "vom Admin erledigt"
        bool AcknowledgedByUser "vom Nutzer gesehen"
    }
```

Datenschutz: Konten enthalten nur Benutzername und Passwort-Hash. Gespeichert werden sonst nur Box, Sensorwerte,
Zeit und technische Ereignisse. Messwerte werden nach 90 Tagen automatisch gelöscht (`Retention:ReadingDays`).

Datenbank-Updates: Passt die Datei nicht mehr zum Modell, sichert der `DatabaseInitializer` sie und übernimmt alle
Daten spaltenweise in das neue Schema – auch im Produktionsbetrieb, ohne Datenverlust.

## Ablauf: Box buchen, parken, abholen

```mermaid
sequenceDiagram
    actor N as Nutzer
    participant F as Dashboard
    participant A as API
    participant P as Pi-Agent
    participant B as Box (Servo, Sensor)

    N->>F: "Box öffnen" (Box 2)
    F->>A: POST /api/boxes/2/book (JWT)
    A->>A: Box 2: Free → OpenForParking
    P->>A: POST /api/sensor-data {slotId:2, distance:80}
    A-->>P: {boxState: OpenForParking, lockOpen: true}
    P->>B: Servo öffnen
    N->>B: Fahrrad einstellen
    loop jede Sekunde
        P->>A: {distance: 3}
    end
    A->>A: 5 s ≤ 5 cm → Locked
    A-->>P: {lockOpen: false}
    P->>B: Servo schließen
    F->>A: GET /api/me (alle 2 s) → "Dein Fahrrad ist sicher verriegelt"
    N->>F: "Box öffnen und Fahrrad abholen"
    F->>A: POST /api/boxes/2/pickup
    A-->>P: {lockOpen: true} → Servo öffnet
    N->>B: Fahrrad entnehmen
    A->>A: 5 s leer → Free, Parkvorgang "Completed"
    A-->>P: {lockOpen: false} → Servo schließt
```

## Ablauf: Fahrrad unerwartet entfernt

```mermaid
sequenceDiagram
    participant P as Pi-Agent
    participant A as API
    participant F as Dashboard (Nutzer)
    participant AD as Admin

    Note over A: Box 2 ist Locked
    P->>A: {distance: 80} (Rad weg, Box nicht geöffnet)
    A->>A: 3 s leer → Blocked, Parkvorgang "BikeRemoved"
    A->>A: Meldung BikeRemoved (Critical, UserId = Besitzer)
    F->>A: GET /api/me
    A-->>F: Alarm → rotes Banner "Dein Fahrrad wurde unerwartet entfernt!"
    AD->>A: prüft vor Ort, POST /api/boxes/2/release → Free
```

## Anomalieerkennung

Mehrere Stufen, damit es nicht nur ein „wenn Vibration, dann Alarm“ ist:

| Stufe | Wo | Wie |
|---|---|---|
| Regel | `SensorDataService` | Vibration → Meldung „Mögliche Manipulation“ (max. 1 pro Minute und Box), geht auch an den Besitzer |
| Zustand | `BoxService` | Fahrrad verschwindet aus verriegelter Box → Alarm „unerwartet entfernt“ |
| Statistisch lernend | `AnomalyDetector` + `AnomalyDetectionWorker` | Lernt pro Box und Zustand Median und Streuung (MAD) von Druck, Abstand und deren Änderungen. Robuster Z-Score ≥ 6 → KI-Anomalie. Vibration allein reicht nicht. |
| Maschinelles Lernen (optional) | `ai/anomaly_service.py` | Isolation Forest (scikit-learn), meldet über `POST /api/anomalies` |

## Auslastungsprognose

`GET /api/forecast` schätzt die freien Plätze der nächsten Stunden aus der Belegung der letzten
28 Tage am selben Wochentag zur selben Stunde. Gibt es dafür zu wenig Daten, wird die gleiche Stunde
über alle Tage genutzt.

## Sicherheit

| Maßnahme | Umsetzung |
|---|---|
| Geräte-Authentifizierung | API-Key im Header `X-Api-Key`, Vergleich in konstanter Zeit |
| Nutzer-Authentifizierung | JWT (HMAC-SHA256, 60 min), Rollen `User` / `Admin` |
| Rechte | Nur der Besitzer öffnet seine Box; Admin-Funktionen nur mit Rolle `Admin`; max. 1 Box pro Nutzer |
| Passwörter | nur PBKDF2-Hash, mind. 8 Zeichen; Admins nur per Kommandozeile |
| Brute Force / Spam | Rate Limiting: 5 Logins pro Minute, 5 Registrierungen pro 10 Minuten und IP |
| Eingabevalidierung | Wertebereiche für Sensorwerte, Benutzername nur `A–Z a–z 0–9 _ . -` |
| Physische Sicherheit | Riegel folgt nur der API; ohne Verbindung bleibt er, wie er ist; unerwartete Entnahme → Alarm + Sperre |
| Fehlerbehandlung | einheitliche ProblemDetails, keine Stacktraces nach außen |
| HTTP-Header | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` |
| Geheimnisse | nicht im Repo, zufällig erzeugt, per Umgebungsvariable |
| Datenschutz | keine Kameras, keine Personendaten, Löschung nach 90 Tagen |

## Tests

| Bereich | Wo | Umfang |
|---|---|---|
| Backend | `api/bikestation/bikestation.Tests` | 56 Tests: Konten, Boxen-Ablauf, Alarm, Rechte, Zeitlimits, Offline, API-Key, Validierung, KI, Statistik, Prognose, Migration |
| Pi-Agent | `pi/test_agent.py` | 7 Tests inkl. Ende-zu-Ende gegen das echte Backend (simulierter Servo) |
| Server-Update | `deploy/update-server.ps1` | an separatem Test-Server geprüft (Update mit Datenerhalt, Abbruch bei kaputter Version) |

```powershell
cd api/bikestation; dotnet test
cd pi; python -m unittest test_agent.py -v
```
