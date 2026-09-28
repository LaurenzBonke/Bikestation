namespace bikestation.Models
{
    public enum ParkingEndReason
    {
        Completed,     // Fahrrad regulär abgeholt
        Cancelled,     // Nutzer hat vor dem Einstellen abgebrochen
        TimedOut,      // kein Fahrrad eingestellt, Box wieder freigegeben
        BikeRemoved    // Fahrrad ohne Öffnen entfernt (Alarm)
    }

    // Ein Parkvorgang: von der Buchung bis das Fahrrad wieder abgeholt ist
    public class Parking
    {
        public int Id { get; set; }
        public int SlotId { get; set; }
        public Slot? Slot { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime BookedAt { get; set; }
        public DateTime? ParkedAt { get; set; }
        public DateTime? PickupRequestedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public ParkingEndReason? EndReason { get; set; }
    }
}
