#!/usr/bin/env python3
"""
Smart Bikestation – Demo ohne Hardware und ohne Docker.

Baut das Dashboard und startet die API im Demo-Modus: virtuelle Station statt Raspberry Pi,
Demo-Konten und Beispieldaten der letzten 14 Tage. Danach öffnet sich der Browser.

    python demo.py                 ->  http://localhost:8080
    python demo.py --port 9000
    python demo.py --reset         # Demo-Datenbank löschen und frisch anfangen

Voraussetzungen: .NET 10 SDK, Node.js 20.19+ (oder 22.12+), Python 3.8+.
Mit Docker geht es ohne diese Voraussetzungen:  docker compose up --build
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import threading
import time
import urllib.request
import webbrowser
from pathlib import Path

ROOT = Path(__file__).resolve().parent
FRONTEND = ROOT / "Frontend" / "api"
BACKEND = ROOT / "api" / "bikestation" / "bikestation"


def require(tool: str, hint: str) -> str:
    path = shutil.which(tool)
    if path is None:
        sys.exit(f"{tool} nicht gefunden – {hint}")
    return path


def build_dashboard(npm: str) -> None:
    if not (FRONTEND / "node_modules").is_dir():
        print("== Abhängigkeiten des Dashboards installieren (einmalig) ...", flush=True)
        subprocess.run([npm, "ci", "--no-fund", "--no-audit"], cwd=FRONTEND, check=True)
    print("== Dashboard bauen ...", flush=True)
    subprocess.run([npm, "run", "build"], cwd=FRONTEND, check=True)


def announce_when_ready(url: str, open_browser: bool) -> None:
    for _ in range(180):
        try:
            with urllib.request.urlopen(f"{url}/api/health", timeout=2):
                break
        except OSError:
            time.sleep(1)
    else:
        return
    print(f"\n  Demo läuft:  {url}   (Beenden: Strg+C)\n", flush=True)
    if open_browser:
        webbrowser.open(url)


def main() -> None:
    parser = argparse.ArgumentParser(description="Smart-Bikestation-Demo ohne Hardware")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--reset", action="store_true", help="Demo-Datenbank löschen")
    parser.add_argument("--no-browser", action="store_true", help="Browser nicht öffnen")
    args = parser.parse_args()

    dotnet = require("dotnet", ".NET 10 SDK installieren: https://dotnet.microsoft.com/download")
    npm = require("npm", "Node.js installieren: https://nodejs.org")

    if args.reset:
        for file in BACKEND.glob("bikestation-demo.db*"):
            file.unlink()
        print("== Demo-Datenbank gelöscht", flush=True)

    build_dashboard(npm)

    url = f"http://localhost:{args.port}"
    threading.Thread(target=announce_when_ready, args=(url, not args.no_browser), daemon=True).start()

    print("== API im Demo-Modus starten (beim ersten Mal wird kompiliert) ...", flush=True)
    command = [
        dotnet, "run", "-c", "Release", "--no-launch-profile", "--",
        "--environment", "Demo",
        "--urls", url,
        "--webroot", str(FRONTEND / "dist"),
    ]
    try:
        subprocess.run(command, cwd=BACKEND, check=True)
    except KeyboardInterrupt:
        print("\nDemo beendet.")
    except subprocess.CalledProcessError as error:
        sys.exit(error.returncode)


if __name__ == "__main__":
    main()
