"""
Simuliert die 3 ESP32 der Smart Bikestation – zum Testen und Vorführen ohne Hardware.

Jeder simulierte Stellplatz schickt alle paar Sekunden Messwerte an die API, genau wie die
echte Firmware (gleiches JSON, gleicher X-Api-Key-Header). Fahrräder kommen und gehen zufällig,
ab und zu wird eine Manipulation (Rütteln) simuliert.

Beispiele:
    python simulator.py --api-key dev-geraete-key-nur-lokal
    python simulator.py --api-key ... --tamper-slot 2        # sofort Manipulation an Platz 2
Nur Python-Standardbibliothek, keine Installation nötig.
"""

from __future__ import annotations

import argparse
import json
import random
import time
import urllib.error
import urllib.request

# ANNAHME: typische Rohwerte – an die echten Sensoren anpassen, sobald kalibriert
FREE_PRESSURE, OCCUPIED_PRESSURE = 40, 850
# Box-Konzept: Fahrrad steht direkt vor dem Ultraschallsensor (<= 5 cm, siehe Box:BikePresentMaxDistanceCm)
FREE_DISTANCE, OCCUPIED_DISTANCE = 80, 3


class SimulatedSlot:
    def __init__(self, slot_id: int):
        self.slot_id = slot_id
        self.occupied = random.random() < 0.5
        self.tamper_ticks = 0

    def tick(self, change_chance: float, tamper_chance: float) -> dict:
        if self.tamper_ticks == 0 and random.random() < change_chance:
            self.occupied = not self.occupied
        if self.tamper_ticks == 0 and random.random() < tamper_chance:
            self.start_tamper()

        pressure = random.gauss(OCCUPIED_PRESSURE if self.occupied else FREE_PRESSURE, 20)
        distance = random.gauss(OCCUPIED_DISTANCE, 0.5) if self.occupied else random.gauss(FREE_DISTANCE, 1.5)
        vibration = False

        if self.tamper_ticks > 0:
            # Rütteln: Druck und Abstand springen, Vibrationssensor schlägt an
            self.tamper_ticks -= 1
            pressure += random.uniform(-600, 250)
            distance += random.uniform(-15, 35)
            vibration = True

        return {
            "slotId": self.slot_id,
            "pressure": int(min(max(pressure, 0), 4095)),
            "distance": int(min(max(distance, 0), 1000)),
            "vibration": vibration,
        }

    def start_tamper(self) -> None:
        self.tamper_ticks = 3
        print(f"  >> Simuliere Manipulation an Stellplatz {self.slot_id}")


def send(api: str, api_key: str, payload: dict) -> str:
    request = urllib.request.Request(
        f"{api.rstrip('/')}/api/sensor-data",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json", "X-Api-Key": api_key},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            return "belegt" if json.load(response).get("occupied") else "frei"
    except urllib.error.HTTPError as error:
        return f"HTTP {error.code}"
    except urllib.error.URLError as error:
        return f"nicht erreichbar ({error.reason})"


def main() -> None:
    parser = argparse.ArgumentParser(description="ESP32-Simulator Smart Bikestation")
    parser.add_argument("--api", default="http://localhost:5137", help="Adresse der C#-API")
    parser.add_argument("--api-key", required=True, help="Wert von Devices:ApiKey")
    parser.add_argument("--slots", type=int, default=3, help="Anzahl Stellplätze")
    parser.add_argument("--interval", type=float, default=2.0, help="Sekunden zwischen Messungen")
    parser.add_argument("--change-chance", type=float, default=0.03, help="Chance pro Messung, dass ein Rad kommt/geht")
    parser.add_argument("--tamper-chance", type=float, default=0.005, help="Chance pro Messung für Manipulation")
    parser.add_argument("--tamper-slot", type=int, help="Direkt beim Start Manipulation an diesem Platz")
    args = parser.parse_args()

    slots = [SimulatedSlot(i) for i in range(1, args.slots + 1)]
    if args.tamper_slot:
        slots[args.tamper_slot - 1].start_tamper()

    print(f"Sende an {args.api} alle {args.interval}s - Strg+C beendet.")
    while True:
        line = []
        for slot in slots:
            payload = slot.tick(args.change_chance, args.tamper_chance)
            result = send(args.api, args.api_key, payload)
            marker = " VIB" if payload["vibration"] else ""
            line.append(f"#{slot.slot_id}: p={payload['pressure']:4d} d={payload['distance']:3d}{marker} -> {result}")
        print(" | ".join(line))
        time.sleep(args.interval)


if __name__ == "__main__":
    main()
