# Smart Bikestation – Architektur

Die Diagramme sind in Mermaid geschrieben und werden auf GitHub direkt als Grafik angezeigt.

## Systemübersicht

```mermaid
flowchart LR
    subgraph Stellplatz["Stellplatz 1–3 (je 1x)"]
        P[SEN0616<br/>Drucksensor] --> E[ESP32]
        U[Ultraschall<br/>Grove V2.0] --> E
        V[Vibrations-<br/>sensor] --> E
        E --> G[LED grün]
        E --> R[LED rot]
    end

    E -- "WLAN · HTTP POST<br/>X-Api-Key" --> API

    subgraph Server["Server (Windows-PC, später Raspberry Pi 5)"]
        API[ASP.NET Core API<br/>Port 8080]
        W1[Anomalieerkennung<br/>Hintergrunddienst]
        W2[Datenaufbewahrung<br/>Hintergrunddienst]
        DB[(SQLite)]
        UI[React-Dashboard<br/>wwwroot]
        API --- DB
        W1 --- DB
        W2 --- DB
        API --> UI
    end

    AI[Python-KI<br/>Isolation Forest<br/>optional] -- "X-Api-Key" --> API
    B[Browser<br/>Nutzer / Admin] -- "HTTP · JWT für Admin" --> API
```

## Datenmodell (ER-Diagramm)

```mermaid
erDiagram
    Slots ||--o{ SensorReadings : "hat"
    Slots ||--o{ Alerts : "hat"

    Slots {
        int Id PK
        string Name
        string Status "Unknown | Free | Occupied"
        datetime LastUpdated "UTC"
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
        string Type "PossibleTampering | SensorMismatch | Anomaly"
        string Severity "Info | Warning | Critical"
        string Message
        double Score "nur KI, 0–1"
        datetime Timestamp "UTC"
        bool Resolved
    }
    AdminUsers {
        int Id PK
        string Username "eindeutig"
        string PasswordHash "PBKDF2"
        datetime CreatedAt
    }
```

Keine personenbezogenen Daten: gespeichert werden nur Stellplatz, Sensorwerte, Zeit und technische
Ereignisse. Messwerte werden nach 90 Tagen automatisch gelöscht (`Retention:ReadingDays`).

## Ablauf: Fahrrad wird abgestellt

```mermaid
sequenceDiagram
    participant S as Sensoren
    participant E as ESP32 (Slot 2)
    participant A as API
    participant D as Datenbank
    participant B as Dashboard

    S->>E: Druck steigt, Ultraschall erkennt Objekt
    E->>E: Druck ≥ Schwellwert → rote LED an
    E->>A: POST /api/sensor-data {slotId:2, pressure:842, distance:27, vibration:false}
    A->>A: API-Key und Werte prüfen
    A->>D: Messwert speichern, Slot 2 = Occupied
    A-->>E: {occupied:true}
    B->>A: GET /api/slots (alle 3 s)
    A-->>B: Stellplatz 2 – Belegt
```

## Ablauf: Manipulation

```mermaid
sequenceDiagram
    participant E as ESP32 (Slot 2)
    participant A as API
    participant W as Anomalieerkennung
    participant D as Datenbank
    participant B as Dashboard

    E->>A: POST /api/sensor-data {vibration:true, pressure:520, distance:44}
    A->>D: Messwert + Meldung "Mögliche Manipulation" (Regel, mit Cooldown)
    W->>D: neue Messwerte lesen (alle 10 s)
    W->>W: mit gelerntem Normalverhalten von Slot 2 vergleichen (robuster Z-Score)
    W->>D: Meldung "KI-Anomalie" mit Score und Begründung
    B->>A: GET /api/alerts
    A-->>B: ⚠ Ungewöhnliche Aktivität an Stellplatz 2
```

## Anomalieerkennung

Zwei Stufen, damit es nicht nur ein „wenn Vibration, dann Alarm“ ist:

| Stufe | Wo | Wie |
|---|---|---|
| Regel | `SensorDataService` | Vibration → Meldung „Mögliche Manipulation“ (max. 1 pro Minute und Platz) |
| Statistisch lernend | `AnomalyDetector` + `AnomalyDetectionWorker` im Backend | Lernt pro Platz und Zustand (frei/belegt) Median und Streuung (MAD) von Druck, Abstand und deren Änderungen. Robuster Z-Score ≥ 6 → Anomalie. Vibration allein reicht nicht, verstärkt aber echte Abweichungen. |
| Maschinelles Lernen (optional) | `ai/anomaly_service.py` | Isolation Forest (scikit-learn) auf denselben Merkmalen, meldet über `POST /api/anomalies` |

Erkannt werden z. B. Rütteln mit Druckabfall bei stehendem Rad oder widersprüchliche Sensoren
(Druck „belegt“, Ultraschall „leer“). Normales Kommen und Gehen löst nichts aus.

## Auslastungsprognose

`GET /api/forecast` schätzt die freien Plätze der nächsten Stunden aus der Belegung der letzten
28 Tage am selben Wochentag zur selben Stunde. Gibt es dafür zu wenig Daten, wird die gleiche Stunde
über alle Tage genutzt.

## Sicherheit

| Maßnahme | Umsetzung |
|---|---|
| Geräte-Authentifizierung | API-Key im Header `X-Api-Key`, Vergleich in konstanter Zeit |
| Admin-Authentifizierung | JWT (HMAC-SHA256, 60 min), Rolle `Admin` wird geprüft |
| Passwörter | nur PBKDF2-Hash, Admin-Anlage nur per Kommandozeile |
| Brute Force | Rate Limiting: 5 Login-Versuche pro Minute und IP |
| Eingabevalidierung | Wertebereiche für alle Sensorwerte, sonst 400 |
| Fehlerbehandlung | einheitliche ProblemDetails, keine Stacktraces nach außen |
| HTTP-Header | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` |
| Geheimnisse | nicht im Repo, zufällig erzeugt, per Umgebungsvariable |
| Datenschutz | keine Kameras, keine Personendaten, Löschung nach 90 Tagen |

## Tests

`api/bikestation/bikestation.Tests`: Integrationstests starten die echte API mit eigener SQLite-Datei
(API-Key, Validierung, Belegung, Meldungen, JWT, Rate Limit, KI-Anomalien, Statistik, Prognose,
Datenaufbewahrung, veraltete Datenbank) und Unit-Tests für den `AnomalyDetector`.

```powershell
cd api/bikestation
dotnet test
```
