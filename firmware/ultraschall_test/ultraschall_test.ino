// Test für den Grove Ultrasonic Ranger V2.0 am ESP32
//
// Der Grove-Sensor hat nur EINEN Signal-Pin (SIG, gelbes Kabel): Über ihn wird der Trigger-Puls
// gesendet UND danach das Echo gemessen. Der Pin NC (weißes Kabel) ist nicht belegt.
// Ist nichts in Reichweite (> ca. 3,5 m), antwortet der Sensor mit einem Puls von ca. 31 ms.
// Serieller Monitor: 115200 Baud.

const int SIG_PIN = 19;  // gelbes SIG-Kabel (am Testaufbau per Pin-Scan bestätigt)
const unsigned long TIMEOUT_US = 40000;  // länger als der 31-ms-Puls für "nichts in Reichweite"
const long MAX_RANGE_CM = 350;

// Einzelpin-Messung wie in der Bikestation-Firmware: Puls senden, dann auf demselben Pin Echo messen
long readDistanceCm() {
  pinMode(SIG_PIN, OUTPUT);
  digitalWrite(SIG_PIN, LOW);
  delayMicroseconds(2);
  digitalWrite(SIG_PIN, HIGH);
  delayMicroseconds(10);
  digitalWrite(SIG_PIN, LOW);
  pinMode(SIG_PIN, INPUT);

  unsigned long duration = pulseIn(SIG_PIN, HIGH, TIMEOUT_US);
  if (duration == 0) {
    return -1;  // Sensor antwortet gar nicht -> Verkabelung/Strom prüfen
  }
  return duration / 29 / 2;  // ca. 29 µs pro cm, hin und zurück
}

void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println("\nUltraschall-Test gestartet (SIG an GPIO19)");
}

void loop() {
  long cm = readDistanceCm();
  if (cm < 0) {
    Serial.println("Keine Antwort vom Sensor - SIG-Kabel und Stromversorgung pruefen");
  } else if (cm > MAX_RANGE_CM) {
    Serial.println("Nichts in Reichweite (> 3,5 m)");
  } else {
    Serial.printf("Entfernung: %ld cm\n", cm);
  }
  delay(300);
}
