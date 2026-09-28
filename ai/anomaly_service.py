"""
KI-Anomalieerkennung für die Smart Bikestation.

Der Dienst lernt aus den gespeicherten Sensordaten, wie "normales" Verhalten an den
Stellplätzen aussieht (Isolation Forest, scikit-learn). Neue Messwerte, die stark davon
abweichen, meldet er als Anomalie an die C#-API (POST /api/anomalies).

Das ist bewusst KEINE feste Regel wie "wenn Vibration, dann Alarm": Das Modell bewertet
die Kombination aus Druck, Abstand, Vibration und deren Änderungen.

Start:
    python anomaly_service.py --api http://localhost:5137 --api-key <Devices:ApiKey>
Selbsttest ohne API:
    python anomaly_service.py --selftest
"""

from __future__ import annotations

import argparse
import json
import logging
import time
import urllib.error
import urllib.request
from dataclasses import dataclass

import numpy as np
from sklearn.ensemble import IsolationForest

log = logging.getLogger("anomaly")

# Reihenfolge der Merkmale im Feature-Vektor
FEATURE_NAMES = ["pressure", "distance", "vibration", "pressure_change", "distance_change"]

# Verständliche Beschreibung für die Meldung im Dashboard
FEATURE_TEXT = {
    "pressure": "ungewöhnlicher Druck",
    "distance": "ungewöhnlicher Abstand",
    "vibration": "Vibration",
    "pressure_change": "starke Druckänderung",
    "distance_change": "starke Abstandsänderung",
}

MIN_TRAINING_SAMPLES = 200
MAX_TRAINING_SAMPLES = 20000


@dataclass
class Reading:
    id: int
    slot_id: int
    pressure: int
    distance: int
    vibration: bool
    timestamp: str


def build_features(readings: list[Reading], previous: dict[int, Reading] | None = None) -> np.ndarray:
    """Wandelt Messwerte in Merkmale um. Änderungen werden je Stellplatz zum vorherigen Wert berechnet."""
    last = dict(previous or {})
    rows = []
    for r in readings:
        prev = last.get(r.slot_id)
        rows.append([
            r.pressure,
            r.distance,
            1.0 if r.vibration else 0.0,
            abs(r.pressure - prev.pressure) if prev else 0.0,
            abs(r.distance - prev.distance) if prev else 0.0,
        ])
        last[r.slot_id] = r
    return np.array(rows, dtype=float)


class AnomalyModel:
    def __init__(self, threshold: float = 0.6):
        # threshold: ab diesem Score (0..1) wird gemeldet
        self.threshold = threshold
        self.model: IsolationForest | None = None
        self.mean = np.zeros(len(FEATURE_NAMES))
        self.std = np.ones(len(FEATURE_NAMES))

    @property
    def trained(self) -> bool:
        return self.model is not None

    def train(self, features: np.ndarray) -> None:
        features = features[-MAX_TRAINING_SAMPLES:]
        self.model = IsolationForest(n_estimators=200, contamination="auto", random_state=42)
        self.model.fit(features)
        # Für die Begründung: welches Merkmal weicht am stärksten vom Durchschnitt ab
        self.mean = features.mean(axis=0)
        self.std = features.std(axis=0) + 1e-6
        log.info("Modell trainiert mit %d Messwerten", len(features))

    def score(self, features: np.ndarray) -> np.ndarray:
        """Anomalie-Score 0..1 (Isolation-Forest-Score nach Liu et al.; > 0.5 = auffällig)."""
        assert self.model is not None
        return -self.model.score_samples(features)

    def reason(self, row: np.ndarray) -> str:
        z = np.abs((row - self.mean) / self.std)
        top = [FEATURE_NAMES[i] for i in np.argsort(z)[::-1][:2] if z[i] > 2]
        return " + ".join(FEATURE_TEXT[name] for name in top) or "ungewöhnliche Kombination der Messwerte"


class ApiClient:
    def __init__(self, base_url: str, api_key: str):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key

    def get_readings(self, after_id: int, limit: int = 5000) -> list[Reading]:
        url = f"{self.base_url}/api/sensor-readings?afterId={after_id}&limit={limit}"
        with urllib.request.urlopen(url, timeout=10) as response:
            data = json.load(response)
        return [
            Reading(d["id"], d["slotId"], d["pressure"], d["distance"], d["vibration"], d["timestamp"])
            for d in data
        ]

    def report_anomaly(self, slot_id: int, score: float, reason: str) -> None:
        body = json.dumps({"slotId": slot_id, "score": round(min(max(score, 0.0), 1.0), 3), "reason": reason})
        request = urllib.request.Request(
            f"{self.base_url}/api/anomalies",
            data=body.encode("utf-8"),
            headers={"Content-Type": "application/json", "X-Api-Key": self.api_key},
            method="POST",
        )
        with urllib.request.urlopen(request, timeout=10) as response:
            result = json.load(response)
        if result.get("created"):
            log.warning("Anomalie gemeldet: Slot %d, Score %.2f (%s)", slot_id, score, reason)


def load_all(api: ApiClient) -> list[Reading]:
    """Holt die komplette Historie seitenweise."""
    readings: list[Reading] = []
    after_id = 0
    while True:
        page = api.get_readings(after_id)
        if not page:
            return readings
        readings.extend(page)
        after_id = page[-1].id


def run(api: ApiClient, model: AnomalyModel, poll_seconds: float, retrain_minutes: float) -> None:
    history: list[Reading] = []
    last_by_slot: dict[int, Reading] = {}
    last_id = 0
    last_training = 0.0

    while True:
        try:
            if not history:
                history = load_all(api)
                last_id = history[-1].id if history else 0
                for r in history:
                    last_by_slot[r.slot_id] = r
                log.info("%d historische Messwerte geladen", len(history))

            # Regelmäßig neu trainieren, damit das Modell sich an neue Muster anpasst
            if len(history) >= MIN_TRAINING_SAMPLES and time.time() - last_training > retrain_minutes * 60:
                model.train(build_features(history))
                last_training = time.time()

            new = api.get_readings(last_id)
            if new:
                features = build_features(new, last_by_slot)
                if model.trained:
                    for reading, row, score in zip(new, features, model.score(features)):
                        if score >= model.threshold:
                            api.report_anomaly(reading.slot_id, float(score), model.reason(row))
                elif len(history) < MIN_TRAINING_SAMPLES:
                    log.info("Noch zu wenig Daten zum Lernen (%d/%d)", len(history), MIN_TRAINING_SAMPLES)

                for r in new:
                    last_by_slot[r.slot_id] = r
                history.extend(new)
                history = history[-MAX_TRAINING_SAMPLES:]
                last_id = new[-1].id

        except (urllib.error.URLError, TimeoutError, ConnectionError) as error:
            log.error("API nicht erreichbar: %s", error)

        time.sleep(poll_seconds)


def selftest() -> None:
    """Prüft das Modell mit künstlichen Daten – ohne API."""
    rng = np.random.default_rng(1)
    normal = []
    for i in range(3000):
        occupied = (i // 40) % 2 == 0
        normal.append(Reading(i, 1 + i % 3,
                              int(rng.normal(850 if occupied else 40, 25)),
                              int(rng.normal(28 if occupied else 80, 2)),
                              False, ""))
    model = AnomalyModel()
    model.train(build_features(normal))

    base = normal[-3:]
    cases = {
        "Normal: Fahrrad steht": Reading(9001, 1, 845, 28, False, ""),
        "Normal: Platz frei": Reading(9002, 2, 42, 81, False, ""),
        "Manipulation: Rütteln + Druck fällt": Reading(9003, 1, 300, 45, True, ""),
        "Unplausibel: Druck hoch, nichts im Abstand": Reading(9004, 3, 900, 80, False, ""),
    }
    previous = {r.slot_id: r for r in base}
    previous[1] = Reading(0, 1, 850, 28, False, "")
    for name, reading in cases.items():
        row = build_features([reading], previous)
        score = model.score(row)[0]
        flag = "ANOMALIE" if score >= model.threshold else "normal"
        print(f"{name:45s} Score {score:.2f} -> {flag}" + (f" ({model.reason(row[0])})" if flag == "ANOMALIE" else ""))


def main() -> None:
    parser = argparse.ArgumentParser(description="KI-Anomalieerkennung Smart Bikestation")
    parser.add_argument("--api", default="http://localhost:5137", help="Adresse der C#-API")
    parser.add_argument("--api-key", default="", help="Wert von Devices:ApiKey")
    parser.add_argument("--threshold", type=float, default=0.6, help="Score ab dem gemeldet wird (0..1)")
    parser.add_argument("--poll", type=float, default=5, help="Abfrageintervall in Sekunden")
    parser.add_argument("--retrain", type=float, default=10, help="Neu trainieren alle X Minuten")
    parser.add_argument("--selftest", action="store_true", help="Modell mit künstlichen Daten testen")
    args = parser.parse_args()

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

    if args.selftest:
        selftest()
        return
    if not args.api_key:
        parser.error("--api-key fehlt")

    run(ApiClient(args.api, args.api_key), AnomalyModel(args.threshold), args.poll, args.retrain)


if __name__ == "__main__":
    main()
