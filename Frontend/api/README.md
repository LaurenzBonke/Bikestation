# Smart Bikestation – Dashboard (React)

Web-App der Smart Bikestation für Nutzer und Admins: freie Boxen sehen, Box buchen und öffnen, Fahrrad abholen,
Alarm bei unerlaubter Entnahme, Statistik mit Prognose und ein Admin-Bereich mit Belegung, Meldungen,
Stationsverwaltung und Box-Protokoll.

Alle Daten kommen aus der C#-API (`api/bikestation`). Eine eigene Datenbank hat das Frontend nicht.
Im Betrieb liefert die API das gebaute Dashboard selbst aus (`npm run build` → `wwwroot`).

## Starten (Entwicklung)

1. Backend starten (im Ordner `api/bikestation/bikestation`):

   ```powershell
   dotnet run                          # normale Entwicklung
   dotnet run --launch-profile demo    # mit virtueller Station, Demo-Konten und Beispieldaten
   ```

   Die API läuft dann auf `http://localhost:5137`.

2. Frontend starten (in diesem Ordner):

   ```powershell
   npm install
   npm run dev
   ```

   Das Dashboard läuft auf `http://localhost:5173`. Der Vite-Dev-Server leitet alle Aufrufe auf `/api` an das
   Backend weiter (siehe `vite.config.ts`). Läuft die API woanders, die Umgebungsvariable `API_URL` setzen:

   ```powershell
   $env:API_URL = "http://<server-ip>:8080"; npm run dev
   ```

## Seiten

| Hash | Seite |
|---|---|
| `#` | Übersicht: freie Stellplätze mit Ort, Klick führt zum Buchen |
| `#boxen` | Box buchen, öffnen, abholen – im Demo-Modus zusätzlich die **virtuelle Station** |
| `#konto` | Anmelden / registrieren – im Demo-Modus mit Demo-Zugängen |
| `#statistik` | Auslastung nach Uhrzeit und Tag, Prognose der nächsten Stunden |
| `#admin` | Belegung, Meldungen, gesperrte Boxen, Stationen verwalten, Box-Protokoll, Beispieldaten |

Der Login liefert einen JWT (Nutzer 12 h, Admins 60 min), der nur im `sessionStorage` des Tabs liegt.

## Sprachen und Darstellung

- **Sprachen:** Deutsch, Englisch, Niederländisch – Auswahl oben rechts, wird im Browser gespeichert.
  Beim ersten Besuch wird die Browsersprache verwendet. Alle Texte stehen in `src/i18n.tsx`;
  TypeScript prüft, dass jede Sprache dieselben Schlüssel hat.
- **Darstellung:** System / Hell / Dunkel. Alle Farben sind CSS-Variablen in `src/style.css`
  mit einem eigenen, kontrastgeprüften Dunkel-Satz.

## Aufbau

| Datei | Aufgabe |
|---|---|
| `src/api.ts` | Typen und Aufrufe der REST-API |
| `src/components/Dashboard.tsx` | Übersicht: freie Plätze, Stationsgrafik |
| `src/components/BoxesPage.tsx` | Buchen, eigener Parkvorgang mit Schritten und Countdown, Verlauf |
| `src/components/DemoStation.tsx` | Virtuelle Station im Demo-Modus: Riegel, LED, Ultraschall, Fahrrad rein/raus |
| `src/components/AccountPage.tsx` | Anmelden, registrieren, Demo-Zugänge, Benachrichtigungen |
| `src/components/AlarmBanner.tsx`, `src/useAlarmSound.ts` | Alarm bei unerlaubter Entnahme (Banner, Ton, Vibration) |
| `src/components/StatisticsPage.tsx`, `ForecastPanel.tsx` | Statistik und Prognose |
| `src/components/AdminPage.tsx` | Admin-Bereich |
| `src/useAuth.ts` | Login, Token-Speicherung, automatisches Abmelden |
| `src/useDemo.ts` | Demo-Modus erkennen, virtuelle Station abfragen |
| `src/i18n.tsx`, `src/useTheme.ts` | Übersetzungen (de/en/nl), Hell-/Dunkelmodus |

## Barrierefreiheit

- Status wird immer auch als Text angezeigt, nicht nur über Farbe
- Textkontrast mindestens 4.5:1, sichtbarer Tastaturfokus, „Zum Inhalt springen“-Link
- Änderungen werden Screenreadern angesagt (`aria-live`), Animationen respektieren „Bewegung reduzieren“
