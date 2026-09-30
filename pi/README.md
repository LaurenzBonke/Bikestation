# Station: Raspberry Pi 5 mit Breadboard

So lief die Station beim Hackathon: Ein Raspberry Pi 5 steuert eine Box. Ultraschallsensor, Servo-Riegel und
grüne LED stecken auf einem Breadboard und hängen direkt an den GPIO-Pins des Pi. Das Programm
`parking_sensor.py` misst, schickt die Messwerte an den Server und stellt Riegel und LED so, wie der Server es
vorgibt.

## Verdrahtung (BCM-Nummern)

| Bauteil | GPIO | Hinweis |
|---|---|---|
| HC-SR04 Trigger | GPIO4 | VCC an 5 V, GND gemeinsam |
| HC-SR04 Echo | GPIO14 | **Spannungsteiler** (z. B. 1 kΩ / 2 kΩ) – Echo liefert 5 V, der Pi verträgt 3,3 V |
| Servo Signal | GPIO15 | Servo-Plus an 5 V, Masse gemeinsam; Pulsbreite 0,5–2,5 ms |
| Grüne LED | GPIO17 | über 330 Ω nach GND; an = frei, aus = Fahrrad eingeschlossen |

## Starten

```bash
sudo apt install -y python3-gpiozero python3-lgpio   # bei Raspberry Pi OS meist schon installiert
cp config.example.ini config.ini                     # API-Key des Servers eintragen
python3 parking_sensor.py                            # oder in Thonny auf "Ausführen"
```

Einstellungen stehen oben im Programm: `SLOT_ID = 1`, `SCHWELLE_CM = 7.0`, Riegelwinkel `WINKEL_OFFEN = 90` und
`WINKEL_GESCHLOSSEN = 45`. Die Server-Adresse ist beim Hackathon `http://192.168.1.198:8080`; mit der
Umgebungsvariable `BIKESTATION_API_URL` lässt sie sich ändern, den API-Key alternativ mit `BIKESTATION_API_KEY`.

## Verhalten

- Jede Sekunde: Abstand messen → `POST /api/sensor-data` → Riegel auf `lockOpen` stellen, LED nach `boxState`.
- Der Servo fährt in 10 kleinen Schritten und wird danach abgeschaltet (kein Zittern, weniger Strom).
- Ob die Box öffnet, verriegelt oder Alarm schlägt, entscheidet allein der Server (Zustandsmaschine).
- **Server nicht erreichbar:** einfache lokale Logik – Objekt näher als 7 cm (3 Messungen hintereinander) → zu,
  sonst auf. Diese Notlogik hat keinen Diebstahlschutz.

## Hinweise

- Pi 5: `RPi.GPIO` funktioniert nicht – das Programm nutzt gpiozero mit `LGPIOFactory`.
- Nur **ein** Programm darf die Pins gleichzeitig nutzen, sonst meldet gpiozero *GPIO busy*.
- Größere Servos besser über ein eigenes 5-V-Netzteil versorgen (Masse verbinden), sonst kann der Pi neu starten.
- Weitere Station: im Admin-Bereich *Station hinzufügen* (bekommt die nächste Nummer), gleiche Hardware aufbauen,
  im Programm `SLOT_ID` auf diese Nummer setzen.
- Ohne Hardware ausprobieren: Demo mit virtueller Station, siehe [README](../README.md#try-the-demo).
