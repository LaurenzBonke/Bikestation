namespace bikestation.Models
{
    public enum SlotStatus
    {
        Unknown,
        Free,
        Occupied
    }

    public class Slot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public SlotStatus Status { get; set; } = SlotStatus.Unknown;

        // Zeitpunkt der letzten Sensormeldung (UTC)
        public DateTime? LastUpdated { get; set; }

        public List<SensorReading> Readings { get; set; } = [];
        public List<Alert> Alerts { get; set; } = [];
    }
}
