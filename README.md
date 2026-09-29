# Smart Bikestation

A lockable bicycle parking station with a web app. Users see which parking spots are free, book a box with
one click, a servo latch opens, the bike is detected by an ultrasonic sensor and the box locks itself. To pick
the bike up they open the box again from the app. If a bike disappears from a locked box without being opened,
the owner gets an alarm (banner + sound) and the box is blocked until an admin checks it.

Built for Hackathon 2026. No cameras, no personal data beyond username and password hash.

This README is the **handover document**: it describes what exists, how it fits together, where everything
runs and how to keep working on it. The sub-folders have their own (German) READMEs with extra detail;
diagrams (state machine, ER model, sequences) are in [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md).

---

## 1. What the system does

**Users** (web app, no install needed)
- Create an account (username + password only) and sign in.
- Overview page: every **free** spot is shown as a card ("Stellplatz 1 is free · 📍 Hackathon Halle").
  Clicking it jumps to the booking page with that box highlighted.
- Book a box → latch opens → put bike in → after 5 s of detection the latch closes ("Locked").
- "Pick up" → latch opens → take bike out → after 5 s the box is free again and the latch closes.
- Alarm if the bike is removed from a locked box without opening it: red banner, repeating chime,
  vibration on Android, blinking tab title. "Got it – stop alarm" acknowledges it and stops everything.
- Languages: German, English, Dutch. Light/dark mode. Keyboard and screen-reader friendly.

**Admins** (`#admin` page, role `Admin`)
- *Who is at which box?* – every station with location, user, since when, state, latch open/closed.
- *Manage stations* – add a station (gets the next number = `slot_id` for the hardware), change its location,
  delete a free station (its readings, alerts and log are deleted with it).
- Open alerts (tampering, AI anomalies, bike removed) → mark as resolved.
- Blocked boxes → release after checking on site.
- Demo data generator (for presentations).
- *Box log* (collapsed dropdown at the bottom) – sentences like
  "anna booked Stellplatz 1 (Hackathon Halle) on 29.09.2026 at 10:41:06 – latch opened", filterable by user.

**Station hardware** – measures distance every second, sends it to the server, and moves the latch exactly as
the server's answer says. All decisions (open/lock/alarm) are made by the server.

**Also included:** statistics page (occupancy by hour/day), occupancy forecast, statistical anomaly detection
built into the backend, optional Python Isolation-Forest service.

---

## 2. Architecture

```
 Station (one per box)                     Server (Windows PC today)                 Browser
 ───────────────────────                   ─────────────────────────────             ──────────────
 Raspberry Pi 5 + HC-SR04 ultrasonic       ASP.NET Core 10 API (C#)  :8080           React dashboard
 + servo latch + green LED          ──►    ├─ BoxService (state machine)     ◄──     (served by the API
 pi/parking_sensor.py                HTTP  ├─ SQLite database                 JWT    from wwwroot)
 (or ESP32 firmware/)           X-Api-Key  ├─ background workers (timeouts,
                                           │  anomaly detection, retention)
   ◄── answer: {boxState, lockOpen} ──     └─ wwwroot = built React app
```

- **One process** serves API and dashboard on port **8080**.
- Devices authenticate with a shared **API key** (header `X-Api-Key`); users/admins with **JWT**
  (users 12 h, admins 60 min).
- The station sends `POST /api/sensor-data {slotId, distance, pressure, vibration}` every second and gets
  `{boxState, lockOpen}` back. That is the whole device protocol.

### Box state machine (heart of the system – `api/bikestation/bikestation/Services/BoxService.cs`)

```
Free ──user books──► OpenForParking ──bike ≤ 7 cm for 5 s──► Locked ──owner "pick up"──► OpenForPickup
  ▲                      │ cancel / 120 s no bike                │                            │ bike gone 5 s
  └──────────────────────┘                                       │                            ▼
  ▲                                                              │ bike gone WITHOUT opening  Free
  └──────── admin releases ◄── Blocked (ALARM) ◄─────────────────┘ (3 s)
OpenForPickup ──120 s, bike still there──► Locked
```

Latch is open only in `OpenForParking` and `OpenForPickup`. Every transition writes a `BoxEvent`
(the admin log). Distances and timings are in `appsettings.json` → `Box`.

### Data model (SQLite, `Data/BikestationDbContext.cs`)

| Table | Purpose |
|---|---|
| `Users` | username, PBKDF2 hash, role `User`/`Admin` |
| `Slots` | one row per station/box: `Id` (= `slot_id` of the hardware), `Location`, sensor status, `BoxState`, active parking |
| `Parkings` | one booking from "booked" to "ended" with end reason |
| `BoxEvents` | admin log: who did what at which box and when |
| `SensorReadings` | raw measurements (deleted after 90 days) |
| `Alerts` | tampering / anomaly / bike removed; resolved by admin, acknowledged by user |

**Schema changes:** there are no EF migrations. `DatabaseInitializer` detects a DB that doesn't match the model,
backs it up (`bikestation.db.vor-update-<timestamp>`) and copies all data column by column into a fresh schema.
So adding a column is safe; renaming one loses that column's data unless you add a mapping (see `RenamedTables`).

---

## 3. Repository layout

| Path | What | Tech |
|---|---|---|
| `api/bikestation/bikestation` | Backend API + state machine + DB | C# / ASP.NET Core 10 / EF Core SQLite |
| `api/bikestation/bikestation.Tests` | 61 integration + unit tests | xUnit, WebApplicationFactory |
| `Frontend/api` | Dashboard (user + admin UI) | React 19, TypeScript, Vite |
| `pi/parking_sensor.py` | **The program that runs on the Pi today** (box 1) | Python 3, gpiozero + lgpio |
| `pi/bikestation_agent.py`, `hardware.py`, `install.sh` | Generic multi-box agent as systemd service (currently **disabled** on the Pi, see §6) | Python |
| `firmware/bikestation_slot` | Alternative station firmware for ESP32 | Arduino C++ |
| `ai/anomaly_service.py` | Optional ML anomaly detection (Isolation Forest) | Python, scikit-learn |
| `tools/simulator.py` | Simulates stations – develop without hardware | Python |
| `deploy/` | Scripts to publish/update/start the server on Windows | PowerShell |
| `docs/ARCHITEKTUR.md` | Mermaid diagrams: system, states, ER model, sequences, security | – |
| `docs/pitch/` | One-page pitch (PDF + HTML source) | – |

---

## 4. Develop locally (no hardware needed)

Prerequisites: .NET 10 SDK, Node.js 20+, Python 3.11+.

```powershell
# 1. Backend  → http://localhost:5137 (API docs: /scalar)
cd api/bikestation/bikestation
dotnet run -- create-admin admin      # once; asks for a password (min. 12 chars)
dotnet run

# 2. Frontend → http://localhost:5173 (proxies /api to :5137)
cd Frontend/api
npm install
npm run dev

# 3. Fake stations (sends readings like real hardware)
python tools/simulator.py --api-key dev-geraete-key-nur-lokal --slots 1
```

Development keys live only in `appsettings.Development.json`. Production refuses to start without real
keys (`Jwt__Key` ≥ 32 chars, `Devices__ApiKey` ≥ 16 chars as environment variables).

### Tests

```powershell
cd api/bikestation; dotnet test          # 61 tests, ~10 s
cd Frontend/api; npx tsc -b; npm run build
cd pi; python -m unittest test_agent.py -v
```

Tests cover accounts, the full box flow, alarm, permissions, timeouts, offline handling, API key, validation,
anomaly detection, statistics, forecast, DB migration, station management and the admin log.
**Run them before every deployment.**

---

## 5. Production server (current setup)

The live server runs on a **Windows 11 PC in the LAN**:

| | |
|---|---|
| URL | `http://192.168.1.198:8080` (from the PC itself: `http://localhost:8080`) |
| Folder | `%USERPROFILE%\Bikestation-Server\` |
| `app\` | running version (`app-alt\` = previous version, `app-neu\` = prepared next version) |
| `data\bikestation.db` | the database (+ automatic backups before schema changes) |
| `config.ps1` | port and **secret keys** (JWT key, device API key) – never commit, never share |
| `logs\server-<date>.log` | server log |
| Admin password / API key | in `Bikestation-Server-Info.txt` on the owner's desktop (not in the repo) |

The server was started **as Administrator**, so stopping/updating it needs an elevated prompt.
Port 8080 must be allowed in the Windows firewall for phones/the Pi to reach it.

### First install on a new PC

```powershell
powershell -ExecutionPolicy Bypass -File deploy\publish.ps1   # builds everything, creates config.ps1 with random keys
%USERPROFILE%\Bikestation-Server\create-admin.cmd admin        # once
%USERPROFILE%\Bikestation-Server\start-server.cmd
```

Put the API key from `config.ps1` into the station program (`pi/parking_sensor.py` reads it from
`/home/bike/bikestation-agent/config.ini`, line `api_key = …`).

### Updating the live server

`deploy\update-server.cmd` → right-click → *Run as administrator*. It builds into `app-neu`, test-starts it
against a **copy** of the database, then swaps `app`↔`app-neu`, restarts and checks `/api/health`;
on failure it rolls back to the old version automatically.

**⚠ Windows Smart App Control** is on for this PC and sometimes blocks freshly built, unsigned DLLs
("Eine Anwendungssteuerungsrichtlinie hat diese Datei blockiert"). The update script then aborts with
*"Neue Version startet im Probelauf nicht"* and the live server is untouched. Because builds are deterministic,
rebuilding the same code gives the same blocked file. Workaround used so far:

1. Publish yourself with a changing version stamp until a build starts:
   `dotnet publish api/bikestation/bikestation/bikestation.csproj -c Release -o %USERPROFILE%\Bikestation-Server\app-neu -p:InformationalVersion=1.0.<timestamp>`
   and try `dotnet bikestation.dll` in that folder (it fails immediately if blocked).
2. Copy `Frontend/api/dist` into `app-neu\wwwroot`.
3. Run `deploy\update-server-ohne-build.cmd` as Administrator (same update, without rebuilding).

Do **not** turn Smart App Control off – on Windows 11 it can only be turned back on by reinstalling Windows.
Long-term fix: run the server on Linux (Raspberry Pi or the planned Proxmox VM).

---

## 6. The station (Raspberry Pi 5)

| | |
|---|---|
| Pi address | `192.168.1.171`, user `bike`, SSH key login |
| Program | `~/Desktop/bikestation/parking_sensor.py` (copy of `pi/parking_sensor.py`) – run it in Thonny or `python3 parking_sensor.py` |
| Old version | `~/Desktop/bikestation/parking_sensor_alt.py` (original student program) |
| API key | read from `/home/bike/bikestation-agent/config.ini` |

**Wiring (BCM numbers – these are fixed, the team chose them):**

| Part | GPIO | Note |
|---|---|---|
| HC-SR04 Trigger | GPIO4 | |
| HC-SR04 Echo | GPIO14 | use a voltage divider, echo is 5 V |
| Servo signal | GPIO15 | servo + at 5 V, common ground; 0.5–2.5 ms pulse |
| Green LED (+330 Ω) | GPIO17 | on = free, off = bike parked and box locked |

Settings at the top of `parking_sensor.py`: `SCHWELLE_CM = 7.0`, latch angles `WINKEL_OFFEN = 90`,
`WINKEL_GESCHLOSSEN = 45`, `SLOT_ID = 1`, `API_URL = "http://192.168.1.198:8080"`.

Behaviour: every second it measures, POSTs to the server and moves the servo to `lockOpen`. The servo moves in
10 small steps and is then detached (no jitter). **If the server is unreachable** it falls back to local logic
(object < 7 cm for 3 readings → close, otherwise open) – note this fallback has no theft protection.

Only **one** program may use the GPIO pins, otherwise you get *"GPIO busy"*. `parking_sensor.py` therefore stops
the systemd user service `bikestation-agent` (the generic agent from `pi/`, currently disabled) on start.
Pi 5 note: `RPi.GPIO` does not work – use gpiozero with `LGPIOFactory`.

**Adding a second station:** Admin page → *Manage stations* → add (e.g. "Mensa") → it gets number 2.
Build the same hardware, copy `parking_sensor.py`, set `SLOT_ID = 2`. Done – the server does the rest.

---

## 7. Configuration reference (`appsettings.json`)

| Key | Default | Meaning |
|---|---|---|
| `Box:StationCount` / `Box:Locations` | 1 / `["Hackathon Halle"]` | only used on the **very first start** (empty DB); afterwards admins manage stations |
| `Box:BikePresentMaxDistanceCm` | 7 | bike counts as present at ≤ this distance |
| `Box:ParkConfirmSeconds` / `LeaveConfirmSeconds` | 5 | debounce before locking / freeing |
| `Box:AlarmConfirmSeconds` | 3 | bike missing from locked box this long → alarm |
| `Box:OpenForParkingTimeoutSeconds` / `OpenForPickupTimeoutSeconds` | 120 | open box timeouts |
| `Devices:ApiKey` | – | shared key for stations and AI service |
| `Devices:OfflineAfterSeconds` | 30 | no reading this long → station offline (booking disabled) |
| `Jwt:Key` | – | JWT signing key |
| `Anomaly:Enabled`, `Anomaly:ZThreshold` | true, 6 | built-in anomaly detection |
| `Retention:ReadingDays` | 90 | delete old sensor readings |
| `Demo:Enabled` | false | allow demo data outside Development |

Any key can be overridden by an environment variable with `__` instead of `:` (e.g. `Box__BikePresentMaxDistanceCm=7`),
which is what `config.ps1` does on the server.

---

## 8. API overview

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/sensor-data` | API key | station sends a reading, gets `{boxState, lockOpen}` |
| GET | `/api/slots`, `/api/slots/{id}` | – | stations with status and location |
| GET | `/api/boxes` | – (JWT adds `isMine`) | boxes with state and location |
| POST | `/api/boxes/{id}/book` · `/cancel` · `/pickup` | JWT | user actions |
| POST | `/api/boxes/{id}/release` | Admin | release a blocked box |
| POST · PUT · DELETE | `/api/boxes` · `/api/boxes/{id}` | Admin | add station / change location / delete free station |
| GET | `/api/boxes/occupancy` | Admin | who is at which box |
| GET | `/api/boxes/events?limit=&slotId=` | Admin | box log |
| GET | `/api/alerts` | Admin or API key | open alerts |
| POST | `/api/alerts/{id}/resolve` | Admin | resolve alert |
| GET | `/api/me`, POST `/api/me/alerts/{id}/ack` | JWT | own box, own alarms, history |
| POST | `/api/auth/register`, `/api/auth/login` | – (rate limited) | accounts |
| GET | `/api/statistics`, `/api/forecast`, `/api/health` | – | statistics, forecast, health check |

Full, always-current list: run the backend in Development and open `/scalar`.

---

## 9. Security & privacy

- Passwords only as PBKDF2 hashes; admins can only be created on the command line (`create-admin`).
- Rate limiting on login (5/min) and registration; input validation everywhere; ProblemDetails errors, no stack traces.
- Only the owner can open their box; one active box per user; admin endpoints require role `Admin`.
- Usernames of other users are only visible to admins; alerts list is admin/device only.
- Secrets never in the repo; production requires them as environment variables.
- No cameras, no personal data besides the account; readings auto-deleted after 90 days.
- **Open issue:** the site runs on plain HTTP in the LAN. Browsers therefore don't allow notifications/push,
  and passwords travel unencrypted inside the LAN. HTTPS is the most important next step for real use.

---

## 10. Known limitations and next steps

| Topic | State | Next step |
|---|---|---|
| HTTPS | missing | reverse proxy with certificate (e.g. Caddy/nginx on the Pi or Proxmox VM) |
| Alarm when phone is on the home screen | not possible without HTTPS | HTTPS + Web Push (service worker, VAPID) |
| Server host | Windows PC + Smart App Control issues | move to Linux (Pi 5 / Proxmox), run as systemd service |
| Red LED | not wired | GPIO27 (pin 13) + 330 Ω, light on `Blocked` |
| Pressure/weight sensor | not connected (Pi has no analog input) | ADS1115 ADC over I²C |
| Offline fallback on the Pi | opens when no object is seen | keep latch closed while server is unreachable |
| DB migrations | custom copy-migration | switch to EF Core migrations when moving to a real DB server |
| Git | work happens on branch `boxen-konten-pi`; PRs go to `main` on `github.com/LaurenzBonke/Bikestation` | merge open work into `main` |

---

## 11. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Pi: `GPIO busy` | another program holds the pins – close Thonny runs / `systemctl --user stop bikestation-agent` |
| Pi: distance always 100 cm | nothing in range, or echo wire / voltage divider loose |
| Servo jitters | normal while powered; program detaches after each move – check common ground and 5 V supply |
| Box can't be booked | station offline (> 30 s without reading) – is `parking_sensor.py` running and the server reachable? |
| Box stuck in `Blocked` | alarm was triggered – admin page → *Blocked boxes* → release |
| Update: "startet im Probelauf nicht" | Smart App Control, see §5 |
| Changes not visible in browser | hard reload (Ctrl+F5); check `app\wwwroot\assets` contains the new `index-*.js` |
| Server won't start: key missing | `config.ps1` missing/incomplete – re-run `deploy\publish.ps1` (keeps existing config) |
