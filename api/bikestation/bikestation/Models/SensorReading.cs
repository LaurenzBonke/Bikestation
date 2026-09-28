namespace bikestation.Models
{
    public class SensorReading
    {
        public int Id { get; set; }
        public int SlotId { get; set; }
        public Slot? Slot { get; set; }

        // Rohwert des SEN0616 (ESP32-ADC, 12 Bit: 0–4095)
        public int Pressure { get; set; }

        // Abstand vom Grove Ultrasonic Ranger in cm
        public int Distance { get; set; }

        public bool Vibration { get; set; }

        // Vom Backend berechnete Belegung zum Zeitpunkt der Messung
        public bool Occupied { get; set; }

        // Zeitstempel wird vom Server gesetzt (UTC), nicht vom ESP32
        public DateTime Timestamp { get; set; }
    }
}
