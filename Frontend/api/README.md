# Smart Bikestation – Dashboard (React)

React-Dashboard für die Smart Bikestation. Es zeigt die Live-Belegung der 3 Stellplätze,
die aktuellen Sensorwerte und offene Meldungen (z. B. „Mögliche Manipulation erkannt“).

Alle Daten kommen aus der C#-API (`api/bikestation`). Eine eigene Datenbank hat das Frontend nicht.

## Starten (Entwicklung)

1. Backend starten (Visual Studio oder im Ordner `api/bikestation/bikestation`):

   ```powershell
   dotnet run
   ```

   Die API läuft dann auf `http://localhost:5137`.

2. Frontend starten (in diesem Ordner):

   ```powershell
   npm install
   npm run dev
   ```

   Das Dashboard läuft auf `http://localhost:5173`. Der Vite-Dev-Server leitet alle Aufrufe
   auf `/api` an das Backend weiter (siehe `vite.config.ts`).

   Läuft die API woanders (z. B. auf dem Raspberry Pi), die Umgebungsvariable `API_URL` setzen:

   ```powershell
   $env:API_URL = "http://192.168.0.50:5137"; npm run dev
   ```

## Admin-Bereich

Unter `#admin` (Link „Admin“ oben) können angemeldete Admins Meldungen als erledigt markieren.
Der Login liefert einen JWT, der 60 Minuten gilt und nur im `sessionStorage` des Tabs liegt.
Einen Admin legt man im Backend an (siehe `api/bikestation/README.md`).

## Aufbau

| Datei | Aufgabe |
|---|---|
| `src/api.ts` | Typen und Aufrufe der REST-API |
| `src/useStationData.ts` | Holt Slots und Meldungen alle 3 Sekunden |
| `src/components/Dashboard.tsx` | Übersicht: freie Plätze, Stationsgrafik |
| `src/components/SlotList.tsx` | Liste der Stellplätze mit Sensorwerten |
| `src/components/AlertsPanel.tsx` | Offene Meldungen |
| `src/components/AdminPage.tsx` | Admin-Login und Meldungen bearbeiten |
| `src/useAuth.ts` | Login, Token-Speicherung, automatisches Abmelden |

## Barrierefreiheit

- Status wird immer auch als Text angezeigt („Stellplatz 1 – Frei“), nicht nur über Farbe
- Textkontrast mindestens 4.5:1, Schriftgrößen ab 11 px
- „Zum Inhalt springen“-Link und sichtbarer Tastaturfokus
- Änderungen der freien Plätze werden Screenreadern angesagt (`aria-live`)
