namespace bikestation.Models
{
    public enum AlertType
    {
        PossibleTampering,
        SensorMismatch,
        Anomaly
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class Alert
    {
        public int Id { get; set; }
        public int SlotId { get; set; }
        public Slot? Slot { get; set; }

        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool Resolved { get; set; }
    }
}
