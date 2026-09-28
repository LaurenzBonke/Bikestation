// Smart Bikestation – Firmware für einen Stellplatz (ESP32)
//
// Liest Drucksensor (SEN0616), Ultraschallsensor (Grove Ultrasonic Ranger V2.0) und
// Vibrationssensor und sendet die Messwerte per WLAN an die C#-API. Die Antwort der API enthält den
// Zustand der Box (frei / offen / verriegelt / gesperrt) – danach richten sich die LEDs und, falls
// angeschlossen, der Servo-Riegel. Entscheiden tut allein das Backend.
// Alle 3 ESP32 bekommen dieselbe Firmware – nur SLOT_ID in config.h ist unterschiedlich.
//
// Board: "ESP32 Dev Module" (Boardpaket "esp32" von Espressif). Keine weiteren Bibliotheken nötig.

#include <WiFi.h>
#include <HTTPClient.h>
#include "config.h"

// ---------- Pins: Standard laut Projektplan, in config.h überschreibbar ----------
#ifndef PIN_PRESSURE
#define PIN_PRESSURE 34    // SEN0616 analog. Muss ein ADC1-Pin sein (32–39), ADC2 geht nicht mit WLAN
#endif
#ifndef PIN_ULTRASONIC
#define PIN_ULTRASONIC 27  // Grove Ultrasonic SIG (ein Pin für Trigger und Echo)
#endif
#ifndef PIN_VIBRATION
#define PIN_VIBRATION 26   // Vibrationssensor digitaler Ausgang
#endif
#ifndef PIN_LED_GREEN
#define PIN_LED_GREEN 25   // grün = frei
#endif
#ifndef PIN_LED_RED
#define PIN_LED_RED 33     // rot = belegt
#endif
// Optional: Servo-Riegel am ESP32 (in config.h PIN_SERVO eintragen). Ohne PIN_SERVO steuert z. B. der Pi den Riegel.
#ifndef SERVO_OPEN_ANGLE
#define SERVO_OPEN_ANGLE 90
#endif
#ifndef SERVO_CLOSED_ANGLE
#define SERVO_CLOSED_ANGLE 0
#endif
// Fahrrad gilt lokal als "da", wenn der Ultraschall höchstens so nah misst – sollte zu Box:BikePresentMaxDistanceCm passen
#ifndef BIKE_PRESENT_MAX_CM
#define BIKE_PRESENT_MAX_CM 5
#endif

// ---------- Zeiten ----------
const unsigned long MEASURE_INTERVAL_MS = 500;   // so oft wird gemessen
const unsigned long SEND_INTERVAL_MS = 2000;     // spätestens so oft wird gesendet (Lebenszeichen)
const unsigned long SEND_INTERVAL_OPEN_MS = 1000; // solange die Box offen ist, schneller (Riegel reagiert zügig)
const unsigned long WIFI_RETRY_MS = 10000;       // so oft wird ein WLAN-Neuverbinden versucht
const unsigned long HTTP_TIMEOUT_MS = 3000;

// Kein Echo vom Ultraschallsensor = nichts im Messbereich
const int NO_ECHO_DISTANCE_CM = 500;

volatile bool vibrationDetected = false;

// Zustand der Box laut API: 'F' frei, 'O' offen (Einstellen/Abholen), 'L' verriegelt, 'B' gesperrt, '?' unbekannt
char boxState = '?';
bool lockOpen = false;
bool lastLocalOccupied = false;   // letzte eigene Messung "Fahrrad da?", um Änderungen sofort zu senden
unsigned long lastMeasure = 0;
unsigned long lastSend = 0;
unsigned long lastWifiAttempt = 0;

// Wird vom Interrupt aufgerufen, damit auch kurze Erschütterungen zwischen zwei Messungen zählen
void IRAM_ATTR onVibration() {
  vibrationDetected = true;
}

// LEDs zeigen den Box-Zustand: grün = frei, rot = verriegelt/gesperrt, grün blinkend = offen
void updateLeds() {
  bool blinkOn = (millis() / 400) % 2 == 0;
  switch (boxState) {
    case 'F': digitalWrite(PIN_LED_GREEN, HIGH); digitalWrite(PIN_LED_RED, LOW); break;
    case 'O': digitalWrite(PIN_LED_GREEN, blinkOn ? HIGH : LOW); digitalWrite(PIN_LED_RED, LOW); break;
    case 'L':
    case 'B': digitalWrite(PIN_LED_GREEN, LOW); digitalWrite(PIN_LED_RED, HIGH); break;
    default:  digitalWrite(PIN_LED_GREEN, LOW); digitalWrite(PIN_LED_RED, blinkOn ? HIGH : LOW); break;  // keine Verbindung
  }
}

#ifdef PIN_SERVO
// Servo per LEDC-PWM (50 Hz, 14 Bit) – ohne zusätzliche Bibliothek
void setServoAngle(int angle) {
  const int pulseUs = 500 + angle * 2000 / 180;           // 0° = 0,5 ms, 180° = 2,5 ms
  ledcWrite(PIN_SERVO, (uint32_t)pulseUs * 16384 / 20000);  // Anteil an 20 ms Periode
}
#endif

void applyLock(bool open) {
#ifdef PIN_SERVO
  static int lastApplied = -1;
  if (lastApplied == (open ? 1 : 0)) return;
  lastApplied = open ? 1 : 0;
  setServoAngle(open ? SERVO_OPEN_ANGLE : SERVO_CLOSED_ANGLE);
  Serial.printf("Riegel %s\n", open ? "GEOEFFNET" : "geschlossen");
#else
  (void)open;
#endif
}

// Mittelwert aus mehreren Messungen gegen Rauschen
int readPressure() {
  long sum = 0;
  const int samples = 10;
  for (int i = 0; i < samples; i++) {
    sum += analogRead(PIN_PRESSURE);
    delay(2);
  }
  return sum / samples;
}

int readDistanceOnce() {
  // Grove Ultrasonic Ranger: kurzer HIGH-Puls startet die Messung, danach Echo auf demselben Pin
  pinMode(PIN_ULTRASONIC, OUTPUT);
  digitalWrite(PIN_ULTRASONIC, LOW);
  delayMicroseconds(2);
  digitalWrite(PIN_ULTRASONIC, HIGH);
  delayMicroseconds(10);
  digitalWrite(PIN_ULTRASONIC, LOW);
  pinMode(PIN_ULTRASONIC, INPUT);

  unsigned long duration = pulseIn(PIN_ULTRASONIC, HIGH, 30000);  // max. 30 ms warten
  if (duration == 0) {
    return NO_ECHO_DISTANCE_CM;
  }
  return (int)(duration / 29 / 2);  // Schallgeschwindigkeit: ca. 29 µs pro cm, hin und zurück
}

// Median aus 3 Messungen, damit einzelne Ausreißer ignoriert werden
int readDistance() {
  int a = readDistanceOnce();
  delay(10);
  int b = readDistanceOnce();
  delay(10);
  int c = readDistanceOnce();
  return max(min(a, b), min(max(a, b), c));
}

void connectWifi() {
  if (WiFi.status() == WL_CONNECTED) return;
  if (lastWifiAttempt != 0 && millis() - lastWifiAttempt < WIFI_RETRY_MS) return;

  lastWifiAttempt = millis();
  Serial.printf("Verbinde mit WLAN \"%s\" ...\n", WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
}

// Sendet die Messwerte. Gibt true zurück, wenn die API geantwortet hat.
bool sendReading(int pressure, int distance, bool vibration) {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("Nicht gesendet: kein WLAN");
    return false;
  }

  char body[128];
  snprintf(body, sizeof(body),
           "{\"slotId\":%d,\"pressure\":%d,\"distance\":%d,\"vibration\":%s}",
           SLOT_ID, pressure, distance, vibration ? "true" : "false");

  HTTPClient http;
  http.setTimeout(HTTP_TIMEOUT_MS);
  http.begin(API_URL);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Api-Key", API_KEY);

  int status = http.POST(body);
  bool ok = status == 200;

  if (ok) {
    // Die API entscheidet über Box-Zustand und Riegel
    String response = http.getString();
    if (response.indexOf("\"boxState\":\"Free\"") >= 0) boxState = 'F';
    else if (response.indexOf("\"boxState\":\"Locked\"") >= 0) boxState = 'L';
    else if (response.indexOf("\"boxState\":\"Blocked\"") >= 0) boxState = 'B';
    else if (response.indexOf("\"boxState\":\"OpenFor") >= 0) boxState = 'O';
    lockOpen = response.indexOf("\"lockOpen\":true") >= 0;
    applyLock(lockOpen);
  } else if (status == 401) {
    Serial.println("API lehnt ab: API_KEY in config.h prüfen");
  } else {
    Serial.printf("Senden fehlgeschlagen: %d (%s)\n", status, http.errorToString(status).c_str());
  }

  http.end();
  return ok;
}

void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.printf("\nSmart Bikestation – Stellplatz %d\n", SLOT_ID);

  pinMode(PIN_LED_GREEN, OUTPUT);
  pinMode(PIN_LED_RED, OUTPUT);
  pinMode(PIN_VIBRATION, INPUT);

  // Messbereich des ADC auf ca. 0–3,1 V stellen (volle Auflösung 0–4095)
  analogReadResolution(12);
  analogSetPinAttenuation(PIN_PRESSURE, ADC_11db);

  attachInterrupt(digitalPinToInterrupt(PIN_VIBRATION), onVibration, VIBRATION_ACTIVE_HIGH ? RISING : FALLING);

  // Kurzer LED-Test beim Start: beide an
  digitalWrite(PIN_LED_GREEN, HIGH);
  digitalWrite(PIN_LED_RED, HIGH);
  delay(500);
  updateLeds();

#ifdef PIN_SERVO
  ledcAttach(PIN_SERVO, 50, 14);
  applyLock(false);  // beim Start verriegelt, bis die API etwas anderes sagt
#endif

  WiFi.mode(WIFI_STA);
  connectWifi();
}

void loop() {
  connectWifi();
  updateLeds();

  if (millis() - lastMeasure < MEASURE_INTERVAL_MS) return;
  lastMeasure = millis();

  int pressure = readPressure();
  int distance = readDistance();
  bool vibration = vibrationDetected;

  // Hat sich "Fahrrad da?" geändert, sofort senden – die API schaltet dann den Riegel
  bool localOccupied = distance <= BIKE_PRESENT_MAX_CM || pressure >= PRESSURE_THRESHOLD;
  bool changed = localOccupied != lastLocalOccupied;
  lastLocalOccupied = localOccupied;

  // Ausgabe für die Kalibrierung im seriellen Monitor (115200 Baud)
  Serial.printf("pressure=%4d distance=%3d cm vib=%d -> %s | Box %c Riegel %s | WLAN %s\n",
                pressure, distance, vibration ? 1 : 0, localOccupied ? "Rad da" : "leer", boxState,
                lockOpen ? "offen" : "zu",
                WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString().c_str() : "getrennt");

  unsigned long interval = boxState == 'O' ? SEND_INTERVAL_OPEN_MS : SEND_INTERVAL_MS;
  if (changed || vibration || millis() - lastSend >= interval) {
    if (sendReading(pressure, distance, vibration)) {
      vibrationDetected = false;
    }
    lastSend = millis();
  }
}
