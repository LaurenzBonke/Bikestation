"""
Bikestation-Agent für den Raspberry Pi.

Liest pro Box den Ultraschallsensor (und optional Vibration/Druck), schickt die Messwerte an die
C#-API (die sie in die Datenbank schreibt) und stellt den Servo-Riegel so, wie die API es vorgibt.

Die Entscheidung "Box öffnen/schließen/Alarm" trifft allein das Backend (Zustandsmaschine) –
der Agent misst nur und führt aus. Fällt die Verbindung aus, bleibt der Riegel, wie er ist.

Start:
    python3 bikestation_agent.py --config config.ini
    python3 bikestation_agent.py --config config.ini --simulate      # ohne Hardware
"""

from __future__ import annotations

import argparse
import configparser
import json
import logging
import signal
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field

import hardware

log = logging.getLogger("agent")


@dataclass
class Box:
    slot_id: int
    distance_sensor: object
    lock: object
    vibration: object | None = None
    last_state: str | None = field(default=None)


class Api:
    def __init__(self, base_url: str, api_key: str, timeout_s: float = 4):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
        self.timeout = timeout_s

    def send_reading(self, slot_id: int, distance: int, vibration: bool, pressure: int = 0) -> dict:
        body = json.dumps({"slotId": slot_id, "pressure": pressure, "distance": distance, "vibration": vibration})
        request = urllib.request.Request(
            f"{self.base_url}/api/sensor-data",
            data=body.encode("utf-8"),
            headers={"Content-Type": "application/json", "X-Api-Key": self.api_key},
            method="POST",
        )
        with urllib.request.urlopen(request, timeout=self.timeout) as response:
            return json.load(response)


class Agent:
    def __init__(self, api: Api, boxes: list[Box], interval_s: float = 1.0):
        self.api = api
        self.boxes = boxes
        self.interval = interval_s
        self.running = True
        self.failures = 0

    def run_once(self) -> None:
        for box in self.boxes:
            distance = box.distance_sensor.read_cm()
            vibration = box.vibration.consume() if box.vibration else False
            try:
                result = self.api.send_reading(box.slot_id, distance, vibration)
            except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as error:
                self.failures += 1
                # Bei Verbindungsproblemen Riegel NICHT verändern – lieber zu als unkontrolliert offen
                if self.failures in (1, 10) or self.failures % 60 == 0:
                    log.warning("API nicht erreichbar (%s) – Riegel bleiben unverändert", error)
                continue

            if self.failures:
                log.info("Verbindung zur API wieder da")
                self.failures = 0

            lock_open = bool(result.get("lockOpen"))
            if box.lock.set_open(lock_open):
                log.info("Box %d: Riegel %s", box.slot_id, "GEÖFFNET" if lock_open else "geschlossen")

            state = result.get("boxState")
            if state != box.last_state:
                log.info("Box %d: %s (Abstand %d cm)", box.slot_id, state, distance)
                box.last_state = state

    def run(self) -> None:
        log.info("Agent läuft: %d Box(en), Intervall %.1f s", len(self.boxes), self.interval)
        while self.running:
            started = time.monotonic()
            self.run_once()
            time.sleep(max(0.0, self.interval - (time.monotonic() - started)))

    def stop(self, *_):
        self.running = False


def build_boxes(config: configparser.ConfigParser, simulate: bool) -> list[Box]:
    boxes = []
    for section in config.sections():
        if not section.startswith("box"):
            continue
        c = config[section]
        slot_id = c.getint("slot_id")
        if simulate:
            sim = hardware.SimulatedBox()
            boxes.append(Box(slot_id, sim, sim, sim))
            continue

        if c.get("sensor", "grove") == "hcsr04":
            sensor = hardware.HcSr04Ultrasonic(c.getint("trigger_pin"), c.getint("echo_pin"))
        else:
            sensor = hardware.GroveUltrasonic(c.getint("sig_pin"))
        lock = hardware.ServoLock(c.getint("servo_pin"), c.getfloat("servo_open_angle", 90), c.getfloat("servo_closed_angle", 0))
        vibration = hardware.VibrationSensor(c.getint("vibration_pin"), c.getboolean("vibration_active_high", True)) \
            if c.get("vibration_pin", "").strip() else None
        boxes.append(Box(slot_id, sensor, lock, vibration))
    if not boxes:
        raise SystemExit("Keine [box...]-Abschnitte in der Konfiguration gefunden")
    return boxes


def main() -> None:
    parser = argparse.ArgumentParser(description="Bikestation-Agent (Raspberry Pi)")
    parser.add_argument("--config", default="config.ini")
    parser.add_argument("--simulate", action="store_true", help="ohne Hardware (Sensoren/Servo simuliert)")
    parser.add_argument("--once", action="store_true", help="nur eine Runde messen und senden (Test)")
    args = parser.parse_args()

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s", stream=sys.stdout)
    config = configparser.ConfigParser()
    if not config.read(args.config, encoding="utf-8"):
        raise SystemExit(f"Konfiguration {args.config} nicht gefunden (Vorlage: config.example.ini)")

    api_cfg = config["api"]
    agent = Agent(
        Api(api_cfg["url"], api_cfg["api_key"]),
        build_boxes(config, args.simulate),
        config.getfloat("agent", "interval_s", fallback=1.0),
    )
    signal.signal(signal.SIGTERM, agent.stop)
    signal.signal(signal.SIGINT, agent.stop)

    if args.once:
        agent.run_once()
    else:
        agent.run()


if __name__ == "__main__":
    main()
