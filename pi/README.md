# Bikestation-Agent (Raspberry Pi)

Das Programm läuft auf dem Raspberry Pi und verbindet die Hardware der Boxen mit dem Server:

- misst pro Box den **Ultraschallsensor** (Grove Ultrasonic Ranger mit einem SIG-Pin oder HC-SR04 mit Trig/Echo),
  optional den **Vibrationssensor**,
- schickt die Messwerte jede Sekunde an die C#-API (`POST /api/sensor-data`) – die API schreibt sie in die Datenbank,
- stellt den **Servo-Riegel** so, wie die API es in der Antwort vorgibt (`lockOpen`).

Ob eine Box öffnet, schließt oder Alarm auslöst, entscheidet allein das Backend (Zustandsmaschine).
Der Agent misst nur und führt aus. Ist die API nicht erreichbar, bleibt der Riegel, wie er ist.

## Ablauf einer Box

```
Frei ──Nutzer klickt "Box öffnen"──> Offen (wartet auf Fahrrad) ──Rad ≤ 5 cm, 5 s lang──> Verriegelt
Verriegelt ──Nutzer klickt "Abholen"──> Offen zum Abholen ──Rad 5 s weg──> Frei
Verriegelt ──Rad weg OHNE Öffnen──> Gesperrt + Alarm an Nutzer und Admin ──Admin gibt frei──> Frei
```

Abstände und Wartezeiten stehen in `appsettings.json` der API unter `Box` und lassen sich am Modell kalibrieren.

## Installation auf dem Pi

```bash
cd ~/Bikestation/pi
cp config.example.ini config.ini     # API-Adresse, API-Key und Pins eintragen
python3 bikestation_agent.py --config config.ini --once   # einmal messen und senden (Test)
bash install.sh                      # als Dienst mit Autostart einrichten
journalctl -u bikestation-agent -f   # Log ansehen
```

## Verdrahtung (pro Box)

| Bauteil | Pi-Anschluss | Hinweis |
|---|---|---|
| Grove Ultrasonic SIG (gelb) | GPIO laut `sig_pin` | NC (weiß) bleibt frei; VCC an **3,3 V** |
| Servo Signal (orange/gelb) | GPIO laut `servo_pin` | Servo-Plus an **5 V**, Masse gemeinsam mit dem Pi |
| Vibrationssensor OUT | GPIO laut `vibration_pin` | optional |

GPIO-Nummern sind BCM-Nummern (z. B. GPIO17 = physischer Pin 11). Größere Servos besser über ein
eigenes 5-V-Netzteil versorgen (Masse verbinden), sonst kann der Pi bei der Bewegung neu starten.

## Tests (auch ohne Pi)

```bash
python -m unittest test_agent.py -v
# Ende-zu-Ende gegen ein laufendes Backend:
BIKESTATION_API=http://localhost:5199 python -m unittest test_agent.py -v
```

Im Simulationsmodus (`--simulate`) läuft der Agent ohne Hardware.
