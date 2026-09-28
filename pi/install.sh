#!/usr/bin/env bash
# Installiert den Bikestation-Agenten als Dienst auf dem Raspberry Pi.
# Aufruf im Ordner pi/:  bash install.sh
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
USER_NAME="$(whoami)"

echo "== Pakete (lgpio für den Pi 5)"
sudo apt-get install -y python3-lgpio

if [ ! -f "$DIR/config.ini" ]; then
  cp "$DIR/config.example.ini" "$DIR/config.ini"
  echo "!! $DIR/config.ini angelegt – bitte API-Key und Pins eintragen, dann erneut ausführen."
  exit 1
fi

echo "== Dienst einrichten"
sed -e "s|__USER__|$USER_NAME|g" -e "s|__DIR__|$DIR|g" "$DIR/bikestation-agent.service" \
  | sudo tee /etc/systemd/system/bikestation-agent.service > /dev/null
sudo systemctl daemon-reload
sudo systemctl enable --now bikestation-agent
sleep 2
systemctl --no-pager status bikestation-agent | head -12
echo "Log ansehen:  journalctl -u bikestation-agent -f"
