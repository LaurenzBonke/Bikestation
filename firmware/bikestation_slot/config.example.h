// Konfiguration für EINEN Stellplatz.
// Diese Datei nach "config.h" kopieren und anpassen. config.h steht in .gitignore,
// damit WLAN-Passwort und API-Key nicht im Repo landen.

#pragma once

// ---------- Pro ESP32 unterschiedlich ----------
#define SLOT_ID 1  // 1, 2 oder 3

// ---------- WLAN ----------
#define WIFI_SSID "Bikestation-WLAN"
#define WIFI_PASSWORD "HIER-WLAN-PASSWORT"

// ---------- API auf dem Raspberry Pi (bzw. eurem Laptop beim Testen) ----------
// IP-Adresse des Rechners, auf dem die C#-API läuft. NICHT "localhost" – das wäre der ESP32 selbst.
// Server-PC (deploy/start-server.cmd): Port 8080. Entwicklung (dotnet run --launch-profile http-lan): Port 5137.
#define API_URL "http://192.168.1.198:8080/api/sensor-data"
// Muss mit Devices:ApiKey der API übereinstimmen (Server: siehe config.ps1 im Server-Ordner)
#define API_KEY "dev-geraete-key-nur-lokal"

// ---------- Pins (nur eintragen, wenn anders verdrahtet als im Projektplan) ----------
// #define PIN_PRESSURE 34      // SEN0616 analog, nur ADC1-Pins 32–39
// #define PIN_ULTRASONIC 27    // Grove Ultrasonic SIG (gelbes Kabel)
// #define PIN_VIBRATION 26
// #define PIN_LED_GREEN 25
// #define PIN_LED_RED 33

// ---------- Optional: Servo-Riegel direkt am ESP32 ----------
// Nur eintragen, wenn der Servo am ESP32 hängt (sonst steuert der Raspberry Pi den Riegel).
// Servo-Plus an 5 V (VIN), Masse gemeinsam, Signal an diesen Pin.
// #define PIN_SERVO 13
// #define SERVO_OPEN_ANGLE 90
// #define SERVO_CLOSED_ANGLE 0

// Fahrrad gilt als "da", wenn der Ultraschall höchstens so nah misst (cm) – wie Box:BikePresentMaxDistanceCm der API
// #define BIKE_PRESENT_MAX_CM 5

// ---------- Sensoren ----------
// ANNAHME: Startwert. Mit dem seriellen Monitor die echten Werte ablesen (frei / mit Fahrrad)
// und einen Wert dazwischen wählen. Sollte zu Occupancy:PressureThreshold der API passen.
#define PRESSURE_THRESHOLD 500

// Vibrationssensor: 1 = Ausgang geht bei Vibration auf HIGH, 0 = geht auf LOW.
// Hängt vom genauen Modul ab – im seriellen Monitor prüfen ("vib=1" beim Klopfen).
#define VIBRATION_ACTIVE_HIGH 1
