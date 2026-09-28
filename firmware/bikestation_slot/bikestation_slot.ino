// Smart Bikestation – Firmware für einen Stellplatz (ESP32)
//
// Liest Drucksensor (SEN0616), Ultraschallsensor (Grove Ultrasonic Ranger V2.0) und
// Vibrationssensor, schaltet die grüne/rote LED und sendet die Messwerte per WLAN an die C#-API.
// Alle 3 ESP32 bekommen dieselbe Firmware – nur SLOT_ID in config.h ist unterschiedlich.
//
// Board: "ESP32 Dev Module" (Boardpaket "esp32" von Espressif). Keine weiteren Bibliotheken nötig.

#include <WiFi.h>
#include <HTTPClient.h>
#include "config.h"

// ---------- Pins laut Projektplan – auf der echten Platine prüfen! ----------
const int PIN_PRESSURE = 34;    // SEN0616 analog. GPIO34 = ADC1, funktioniert auch bei aktivem WLAN
const int PIN_ULTRASONIC = 27;  // Grove Ultrasonic SIG (ein Pin für Trigger und Echo)
const int PIN_VIBRATION = 26;   // Vibrationssensor digitaler Ausgang
const int PIN_LED_GREEN = 25;   // grün = frei
const int PIN_LED_RED = 33;     // rot = belegt

// ---------- Zeiten ----------
const unsigned long MEASURE_INTERVAL_MS = 500;   // so oft wird gemessen
const unsigned long SEND_INTERVAL_MS = 5000;     // spätestens so oft wird gesendet (Lebenszeichen)
const unsigned long WIFI_RETRY_MS = 10000;       // so oft wird ein WLAN-Neuverbinden versucht
const unsigned long HTTP_TIMEOUT_MS = 3000;

// Kein Echo vom Ultraschallsensor = nichts im Messbereich
const int NO_ECHO_DISTANCE_CM = 500;

volatile bool vibrationDetected = false;

bool occupied = false;            // aktueller Zustand (LEDs), kann von der API-Antwort überschrieben werden
bool lastLocalOccupied = false;   // letzte eigene Entscheidung, um Änderungen zu erkennen
unsigned long lastMeasure = 0;
unsigned long lastSend = 0;
unsigned long lastWifiAttempt = 0;

// Wird vom Interrupt aufgerufen, damit auch kurze Erschütterungen zwischen zwei Messungen zählen
void IRAM_ATTR onVibration() {
  vibrationDetected = true;
}

void setLeds(bool isOccupied) {
  digitalWrite(PIN_LED_GREEN, isOccupied ? LOW : HIGH);
  digitalWrite(PIN_LED_RED, isOccupied ? HIGH : LOW);
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
    // Die API entscheidet endgültig über "belegt" (Schwellwert zentral einstellbar)
    String response = http.getString();
    if (response.indexOf("\"occupied\":true") >= 0) occupied = true;
    if (response.indexOf("\"occupied\":false") >= 0) occupied = false;
    setLeds(occupied);
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
  setLeds(false);

  WiFi.mode(WIFI_STA);
  connectWifi();
}

void loop() {
  connectWifi();

  if (millis() - lastMeasure < MEASURE_INTERVAL_MS) return;
  lastMeasure = millis();

  int pressure = readPressure();
  int distance = readDistance();
  bool vibration = vibrationDetected;

  // Lokale Entscheidung, damit die LED sofort reagiert – auch ohne WLAN
  // Verglichen wird mit der letzten eigenen Entscheidung (nicht mit der API-Antwort),
  // sonst würde die LED springen, falls die Schwellwerte von ESP32 und API abweichen.
  bool localOccupied = pressure >= PRESSURE_THRESHOLD;
  bool changed = localOccupied != lastLocalOccupied;
  if (changed) {
    lastLocalOccupied = localOccupied;
    occupied = localOccupied;
    setLeds(occupied);
  }

  // Ausgabe für die Kalibrierung im seriellen Monitor (115200 Baud)
  Serial.printf("pressure=%4d distance=%3d cm vib=%d -> %s | WLAN %s\n",
                pressure, distance, vibration ? 1 : 0, occupied ? "belegt" : "frei",
                WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString().c_str() : "getrennt");

  // Sofort senden bei Änderung oder Vibration, sonst regelmäßig als Lebenszeichen
  if (changed || vibration || millis() - lastSend >= SEND_INTERVAL_MS) {
    if (sendReading(pressure, distance, vibration)) {
      vibrationDetected = false;
    }
    lastSend = millis();
  }
}
