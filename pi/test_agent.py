"""
Tests für den Bikestation-Agenten – laufen ohne Raspberry Pi:
    python -m unittest test_agent.py -v
Der Ende-zu-Ende-Test läuft nur, wenn BIKESTATION_API auf ein laufendes Test-Backend zeigt, z. B.
    set BIKESTATION_API=http://localhost:5199 & python -m unittest test_agent.py -v
"""

import json
import os
import threading
import time
import unittest
import urllib.request
from http.server import BaseHTTPRequestHandler, HTTPServer

import hardware
from bikestation_agent import Agent, Api, Box


class StubApi(BaseHTTPRequestHandler):
    """Tut so, als wäre es die C#-API: merkt sich Anfragen, antwortet mit vorgegebenem Riegelzustand."""
    requests: list = []
    lock_open = False

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        StubApi.requests.append((self.path, self.headers.get("X-Api-Key"), body))
        payload = json.dumps({"slotId": body["slotId"], "occupied": False, "status": "Free",
                              "boxState": "OpenForParking" if StubApi.lock_open else "Free",
                              "lockOpen": StubApi.lock_open}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, *_):
        pass


class AgentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = HTTPServer(("127.0.0.1", 0), StubApi)
        threading.Thread(target=cls.server.serve_forever, daemon=True).start()
        cls.url = f"http://127.0.0.1:{cls.server.server_port}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()

    def setUp(self):
        StubApi.requests = []
        StubApi.lock_open = False

    def test_sendet_messwerte_mit_api_key(self):
        sim = hardware.SimulatedBox()
        sim.distance_cm = 4
        sim.vibration = True
        Agent(Api(self.url, "geheim"), [Box(2, sim, sim, sim)]).run_once()

        path, key, body = StubApi.requests[0]
        self.assertEqual("/api/sensor-data", path)
        self.assertEqual("geheim", key)
        self.assertEqual({"slotId": 2, "pressure": 0, "distance": 4, "vibration": True}, body)
        self.assertFalse(sim.vibration, "Vibration wird nach dem Senden zurückgesetzt")

    def test_riegel_folgt_der_api(self):
        sim = hardware.SimulatedBox()
        agent = Agent(Api(self.url, "k"), [Box(1, sim, sim)])
        agent.run_once()
        self.assertFalse(sim.is_open)
        StubApi.lock_open = True
        agent.run_once()
        agent.run_once()
        self.assertTrue(sim.is_open)
        self.assertEqual([False, True], sim.moves, "Servo bewegt sich nur bei Änderung")

    def test_ohne_verbindung_bleibt_riegel_unveraendert(self):
        sim = hardware.SimulatedBox()
        sim.is_open = True
        Agent(Api("http://127.0.0.1:9", "k", timeout_s=0.5), [Box(1, sim, sim)]).run_once()
        self.assertTrue(sim.is_open)
        self.assertEqual([], sim.moves)


class EchoTests(unittest.TestCase):
    def test_kein_echo_liefert_nichts_in_reichweite(self):
        self.assertEqual(hardware.NO_ECHO_CM, hardware._measure_echo(lambda: 0, 0.005))

    def test_pulslaenge_wird_in_cm_umgerechnet(self):
        # 580 µs HIGH ≈ 10 cm
        start = time.perf_counter_ns()

        def read():
            t = (time.perf_counter_ns() - start) / 1000
            return 1 if 100 <= t < 680 else 0

        self.assertAlmostEqual(10, hardware._measure_echo(read, 0.04), delta=1)

    def test_median_ignoriert_ausreisser(self):
        values = iter([4, 500, 5])
        sensor = type("S", (), {"read_cm": lambda self: next(values)})()
        self.assertEqual(5, hardware.median_distance(sensor, pause_s=0))


@unittest.skipUnless(os.environ.get("BIKESTATION_API"), "BIKESTATION_API nicht gesetzt")
class EndToEndTests(unittest.TestCase):
    """Echtes Backend: Nutzer bucht Box 3, simuliertes Fahrrad kommt und geht, Servo folgt."""

    def call(self, method, path, token=None, body=None):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = f"Bearer {token}"
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(f"{self.base}{path}", data=data, headers=headers, method=method)
        with urllib.request.urlopen(request, timeout=5) as response:
            raw = response.read()
            return json.loads(raw) if raw else None

    def wait_for(self, agent, sim, predicate, seconds=15):
        end = time.monotonic() + seconds
        while time.monotonic() < end:
            agent.run_once()
            if predicate():
                return True
            time.sleep(0.5)
        return False

    def test_buchen_parken_abholen(self):
        self.base = os.environ["BIKESTATION_API"]
        key = os.environ.get("BIKESTATION_KEY", "dev-geraete-key-nur-lokal")
        user = f"agenttest{int(time.time())}"
        token = self.call("POST", "/api/auth/register", body={"username": user, "password": "agent-test-passwort"})["token"]

        sim = hardware.SimulatedBox()
        agent = Agent(Api(self.base, key), [Box(3, sim, sim)])
        agent.run_once()
        self.assertFalse(sim.is_open, "freie Box ist zu")

        self.call("POST", "/api/boxes/3/book", token)
        self.assertTrue(self.wait_for(agent, sim, lambda: sim.is_open), "nach Buchung öffnet der Riegel")

        sim.distance_cm = 3  # Fahrrad eingestellt
        self.assertTrue(self.wait_for(agent, sim, lambda: sim.is_open is False), "Riegel schließt, wenn das Rad erkannt ist")

        self.call("POST", "/api/boxes/3/pickup", token)
        self.assertTrue(self.wait_for(agent, sim, lambda: sim.is_open), "Riegel öffnet zum Abholen")

        sim.distance_cm = 80  # Fahrrad entnommen
        self.assertTrue(self.wait_for(agent, sim, lambda: sim.is_open is False), "Riegel schließt, Box wieder frei")
        me = self.call("GET", "/api/me", token)
        self.assertIsNone(me["parking"])
        self.assertEqual("Completed", me["history"][0]["endReason"])


if __name__ == "__main__":
    unittest.main()
