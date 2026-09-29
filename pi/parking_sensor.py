#!/usr/bin/env python3
"""
Smart Bikestation – Box 1: Ultraschall + Servo-Riegel, verbunden mit dem Server.

Pins wie im ursprünglichen parking_sensor.py: Trigger GPIO4, Echo GPIO14, Servo GPIO15.

- Server erreichbar: Der Server entscheidet (Box buchen -> Riegel auf, Fahrrad erkannt -> zu,
  Abholen -> auf, Fahrrad weg -> zu, Alarm bei unerwarteter Entnahme). Messwerte landen in der Datenbank.
- Server NICHT erreichbar: alte Logik – Objekt näher als SCHWELLE_CM -> zu, sonst auf.

Starten: in Thonny auf "Ausführen" oder  python3 parking_sensor.py
Nur EIN Programm darf die Pins nutzen – dieses Programm stoppt deshalb den Hintergrund-Dienst.
"""

import json
import subprocess
import time
import urllib.error
import urllib.request
import warnings

from gpiozero import LED, AngularServo, DistanceSensor
from gpiozero.pins.lgpio import LGPIOFactory

# Hinweise von gpiozero zu pigpio sind hier unwichtig
warnings.filterwarnings("ignore", module="gpiozero")

# ---------------------------------------------------------------- Einstellungen
TRIGGER_PIN = 4
ECHO_PIN = 14
SERVO_PIN = 15
LED_GRUEN_PIN = 17   # grün = frei; aus, sobald ein Fahrrad geparkt und die Tür zu ist
SCHWELLE_CM = 7.0

# Winkel aus "test servo.py" (am Modell ausprobiert)
WINKEL_OFFEN = 100
WINKEL_GESCHLOSSEN = 50
BEWEGUNGSDAUER_S = 0.6   # Zeit, bis der Servo sicher angekommen ist
SCHRITTE = 10            # sanft in kleinen Schritten fahren statt ruckartig
LOKAL_BESTAETIGUNG = 3   # ohne Server: so viele gleiche Messungen hintereinander, bevor die Schranke fährt

SLOT_ID = 1
API_URL = "http://192.168.1.198:8080"
API_KEY_DATEI = "/home/bike/bikestation-agent/config.ini"  # API-Key steht dort (api_key = ...)
INTERVALL_S = 1.0


def api_key_lesen() -> str:
    try:
        for zeile in open(API_KEY_DATEI, encoding="utf-8"):
            if zeile.strip().startswith("api_key"):
                return zeile.split("=", 1)[1].strip()
    except OSError:
        pass
    return ""


def dienst_stoppen() -> None:
    """Der Hintergrund-Dienst würde dieselben Pins belegen ('GPIO busy') – vorher anhalten."""
    subprocess.run(["systemctl", "--user", "stop", "bikestation-agent"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def an_server_senden(abstand_cm: int, api_key: str):
    body = json.dumps({"slotId": SLOT_ID, "pressure": 0, "distance": abstand_cm, "vibration": False}).encode()
    anfrage = urllib.request.Request(f"{API_URL}/api/sensor-data", data=body, method="POST",
                                     headers={"Content-Type": "application/json", "X-Api-Key": api_key})
    try:
        with urllib.request.urlopen(anfrage, timeout=3) as antwort:
            return json.load(antwort)
    except (urllib.error.URLError, TimeoutError, OSError, ValueError):
        return None


def main() -> None:
    dienst_stoppen()
    time.sleep(0.5)

    sensor = DistanceSensor(echo=ECHO_PIN, trigger=TRIGGER_PIN, max_distance=1.0, pin_factory=LGPIOFactory())
    servo = AngularServo(SERVO_PIN, min_angle=0, max_angle=180,
                         min_pulse_width=0.5 / 1000, max_pulse_width=2.5 / 1000, pin_factory=LGPIOFactory())
    led_gruen = LED(LED_GRUEN_PIN, pin_factory=LGPIOFactory())
    led_gruen.on()
    api_key = api_key_lesen()
    riegel_offen = None
    riegel_winkel = None
    lokal_zaehler = 0
    letzter_zustand = None
    letzte_ausgabe = 0.0

    def riegel(offen: bool) -> None:
        nonlocal riegel_offen, riegel_winkel
        if riegel_offen == offen:
            return
        ziel = WINKEL_OFFEN if offen else WINKEL_GESCHLOSSEN
        if riegel_winkel is None:
            servo.angle = ziel  # Startposition unbekannt -> direkt anfahren
        else:
            for i in range(1, SCHRITTE + 1):
                servo.angle = riegel_winkel + (ziel - riegel_winkel) * i / SCHRITTE
                time.sleep(0.03)
        time.sleep(BEWEGUNGSDAUER_S)
        servo.detach()  # Signal aus: kein Zittern, weniger Strom
        riegel_offen, riegel_winkel = offen, ziel
        print("Riegel GEÖFFNET" if offen else "Riegel geschlossen")

    print("Überwachung gestartet (Strg+C zum Beenden) ...")
    if not api_key:
        print("Kein API-Key gefunden – arbeite nur lokal.")
    time.sleep(0.5)  # erste Messungen abwarten

    try:
        while True:
            abstand_cm = sensor.distance * 100
            antwort = an_server_senden(int(round(abstand_cm)), api_key) if api_key else None

            if antwort is not None:
                zustand = antwort.get("boxState", "?")
                riegel(bool(antwort.get("lockOpen")))
                # Grün aus, solange ein Fahrrad eingeschlossen ist (oder die Box nach Alarm gesperrt ist)
                geparkt = zustand in ("Locked", "Blocked")
                modus = f"Server: {zustand}"
            else:
                # Ohne Server: alte Logik, aber erst nach mehreren gleichen Messungen (keine Ausreißer)
                erkannt = abstand_cm < SCHWELLE_CM
                aktuell = riegel_offen is False
                lokal_zaehler = lokal_zaehler + 1 if erkannt != aktuell else 0
                if riegel_offen is None or lokal_zaehler >= LOKAL_BESTAETIGUNG:
                    riegel(not erkannt)
                    lokal_zaehler = 0
                geparkt = riegel_offen is False
                modus = "lokal (Server nicht erreichbar)"

            if geparkt:
                led_gruen.off()
            else:
                led_gruen.on()

            # Bei Änderung sofort, sonst alle 5 Sekunden eine Statuszeile
            if modus != letzter_zustand or time.monotonic() - letzte_ausgabe >= 5:
                print(f"{time.strftime('%H:%M:%S')}  Abstand {abstand_cm:5.1f} cm  |  {modus}")
                letzter_zustand = modus
                letzte_ausgabe = time.monotonic()
            time.sleep(INTERVALL_S)
    finally:
        led_gruen.off()
        led_gruen.close()
        servo.detach()
        servo.close()
        sensor.close()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nBeendet.")
