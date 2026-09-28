# Smart Bikestation – ESP32-Firmware

Eine Firmware für alle 3 Stellplätze. Der einzige Unterschied ist `SLOT_ID` in `config.h`.

## 1. ESP32 am PC erkennbar machen (Treiber)

Die Boards nutzen einen **CP2102**-USB-Chip (Silicon Labs). Windows bringt dafür oft keinen Treiber mit,
dann taucht im Geräte-Manager „CP2102 USB to UART Bridge Controller“ mit gelbem Warndreieck auf
(Fehlercode 28) und es gibt **keinen COM-Port**.

1. Auf der Seite von Silicon Labs den **„CP210x Universal Windows Driver“** herunterladen
   (silabs.com → Suche „CP210x VCP Drivers“).
2. ZIP entpacken → Rechtsklick auf `silabser.inf` → **Installieren**.
3. ESP32 ab- und wieder anstecken. Im Geräte-Manager unter **Anschlüsse (COM & LPT)** steht jetzt
   „Silicon Labs CP210x USB to UART Bridge (COMx)“. Diese COM-Nummer braucht ihr in der Arduino IDE.

Wird gar nichts angezeigt: anderes USB-Kabel testen – viele Kabel können nur laden, keine Daten.

## 2. Arduino IDE einrichten

1. Arduino IDE 2 installieren.
2. *Datei → Einstellungen → Zusätzliche Boardverwalter-URLs*:
   `https://espressif.github.io/arduino-esp32/package_esp32_index.json`
3. *Werkzeuge → Board → Boardverwalter* → **„esp32“ von Espressif Systems** installieren.
4. *Werkzeuge → Board* → **„ESP32 Dev Module“**, *Werkzeuge → Port* → den COM-Port von oben.

## 3. Konfigurieren und hochladen

1. `bikestation_slot/config.example.h` nach `bikestation_slot/config.h` kopieren.
2. In `config.h` eintragen: `SLOT_ID`, WLAN-Name/-Passwort, IP-Adresse der API, `API_KEY`.
   - Die API muss im Netzwerk erreichbar sein: auf dem Laptop mit dem Launch-Profil **`http-lan`** starten
     (lauscht auf `0.0.0.0:5137`) und ggf. die Windows-Firewall für Port 5137 freigeben.
   - `API_URL` ist die IP des Rechners mit der API (`ipconfig`), **nicht** `localhost`.
3. `bikestation_slot.ino` öffnen → **Hochladen**. Hängt es bei „Connecting….“: die **BOOT**-Taste
   am ESP32 gedrückt halten, bis der Upload startet.
4. *Werkzeuge → Serieller Monitor*, **115200 Baud**. Jede halbe Sekunde erscheint z. B.:
   `pressure= 842 distance= 27 cm vib=0 -> belegt | WLAN 192.168.0.51`

## 4. Verdrahtung pro Stellplatz

> ⚠ Die GPIOs stammen aus dem Projektplan. Vor dem Anschließen mit dem Pinout eurer konkreten
> ESP32-Platine vergleichen. Die Pins 34, 27, 26, 25, 33 sind keine Boot-Pins und laufen mit WLAN.

| Bauteil | Anschluss | ESP32 |
|---|---|---|
| SEN0616 Drucksensor | Analog-Ausgang | **GPIO34** |
| | VCC / GND | 3V3 / GND (siehe Hinweis unten) |
| Grove Ultrasonic Ranger V2.0 | SIG (gelbes Kabel) | **GPIO27** |
| | VCC (rot) / GND (schwarz) | 3V3 / GND |
| | NC (weiß) | nicht anschließen |
| Vibrationssensor | OUT / DO | **GPIO26** |
| | VCC / GND | 3V3 / GND |
| Grüne LED | GPIO → **220–330 Ω** → Anode (langes Bein), Kathode → GND | **GPIO25** |
| Rote LED | GPIO → **220–330 Ω** → Anode (langes Bein), Kathode → GND | **GPIO33** |

```
            ESP32
          ┌────────┐
 SEN0616 ─┤ GPIO34 │
 Ultrasch.┤ GPIO27 │
 Vibration┤ GPIO26 │
          │ GPIO25 ├──[330Ω]──►|── GND   (grün)
          │ GPIO33 ├──[330Ω]──►|── GND   (rot)
          │    3V3 ├── VCC aller Sensoren
          │    GND ├── GND aller Sensoren und LEDs (gemeinsame Masse!)
          └────────┘
```

### Hinweise zu den Sensoren (bitte prüfen)

- **SEN0616:** Der ESP32-ADC verträgt **höchstens 3,3 V**. Im Datenblatt prüfen, welche Spannung der
  Analog-Ausgang maximal liefert. Wenn das Modul mit 3,3 V betrieben werden kann, an 3V3 anschließen –
  dann bleibt auch der Ausgang unter 3,3 V. Bei einem 5-V-Modul einen Spannungsteiler verwenden.
- **Vibrationssensor:** Der genaue Typ ist noch offen. Im seriellen Monitor auf den Sensor klopfen:
  erscheint `vib=1`, passt es. Sonst in `config.h` `VIBRATION_ACTIVE_HIGH` auf `0` setzen.
  Viele Module haben ein Poti für die Empfindlichkeit.
- **Ultraschall:** Liefert er kein Echo, sendet die Firmware 500 cm (= nichts im Messbereich).

## 5. Kalibrieren

1. Seriellen Monitor öffnen, Stellplatz leer: `pressure`-Wert notieren (z. B. ~40).
2. Fahrrad (bzw. Modell-Gewicht) draufstellen: Wert notieren (z. B. ~850).
3. Einen Wert dazwischen wählen (z. B. 500) und eintragen:
   - `PRESSURE_THRESHOLD` in `config.h` (für die LED)
   - `Occupancy:PressureThreshold` in `appsettings.json` der API (für Dashboard/Statistik)

## Verhalten

- Misst alle 0,5 s. Sendet sofort, wenn ein Fahrrad kommt oder geht oder Vibration erkannt wird,
  sonst alle 2 s (solange die Box offen ist: jede Sekunde, damit der Riegel zügig reagiert).
  Meldet ein ESP32 länger als 30 s nicht, zeigt das Dashboard die Box als „Offline“.
- Die API antwortet mit dem Zustand der Box. Danach richten sich die LEDs:

  | LED | Bedeutung |
  |---|---|
  | grün | Box frei |
  | grün blinkend | Box offen – Fahrrad einstellen bzw. abholen |
  | rot | Box verriegelt (oder nach Alarm gesperrt) |
  | rot blinkend | noch keine Verbindung zur API |

- Optional steuert die Firmware einen **Servo-Riegel** (`PIN_SERVO` in `config.h`). Ohne `PIN_SERVO`
  übernimmt der Raspberry Pi den Riegel (siehe `pi/`).
- Verbindet sich bei WLAN-Abbruch automatisch neu.
