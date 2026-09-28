namespace bikestation.Models
{
    // Belegung laut Sensor (Anzeige im Dashboard)
    public enum SlotStatus
    {
        Unknown,
        Free,
        Occupied
    }

    // Zustand der abschließbaren Box – steuert den Servo-Riegel
    public enum BoxState
    {
        Free,            // leer, geschlossen, kann gebucht werden
        OpenForParking,  // gebucht, Riegel offen, wartet auf das Fahrrad
        Locked,          // Fahrrad erkannt, Riegel zu
        OpenForPickup,   // Besitzer hat geöffnet, wartet bis das Fahrrad weg ist
        Blocked          // Fahrrad ohne Öffnen entfernt – Admin muss die Box prüfen und freigeben
    }

    // Ein Stellplatz = eine Box mit Sensoren und Servo-Riegel
    public class Slot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public SlotStatus Status { get; set; } = SlotStatus.Unknown;

        // Zeitpunkt der letzten Sensormeldung (UTC)
        public DateTime? LastUpdated { get; set; }

        public BoxState BoxState { get; set; } = BoxState.Free;
        public DateTime? BoxStateChangedAt { get; set; }

        // Seit wann der Ultraschallsensor ununterbrochen ein Fahrrad bzw. keins sieht –
        // damit kurze Messausreißer nicht sofort den Riegel schalten
        public DateTime? BikePresentSince { get; set; }
        public DateTime? BikeAbsentSince { get; set; }

        // Aktueller Parkvorgang (gebucht, geparkt oder beim Abholen) – bewusst ohne Navigation,
        // um eine zirkuläre Beziehung Box <-> Parkvorgang zu vermeiden
        public int? ActiveParkingId { get; set; }

        public List<SensorReading> Readings { get; set; } = [];
        public List<Alert> Alerts { get; set; } = [];

        // Riegel offen, solange die Box auf das Einstellen oder Abholen wartet
        public bool LockOpen => BoxState is BoxState.OpenForParking or BoxState.OpenForPickup;
    }
}
