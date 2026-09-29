namespace bikestation.Models
{
    public enum BoxEventType
    {
        Booked,           // Nutzer hat gebucht – Riegel öffnet
        Cancelled,        // Buchung abgebrochen – Riegel schließt
        Parked,           // Fahrrad erkannt – Riegel schließt
        PickupRequested,  // Nutzer öffnet zum Abholen – Riegel öffnet
        PickedUp,         // Fahrrad entnommen – Riegel schließt, Box frei
        ParkingTimedOut,  // kein Fahrrad eingestellt – Riegel schließt, Box frei
        PickupTimedOut,   // Fahrrad nicht entnommen – Riegel schließt wieder
        BikeRemoved,      // Alarm: Fahrrad ohne Öffnen entfernt – Box gesperrt
        Released          // Admin hat die gesperrte Box freigegeben
    }

    // Protokoll aller Aktionen an den Boxen (wer, wann, welche Box, Riegel auf/zu) – für Admins
    public class BoxEvent
    {
        public int Id { get; set; }
        public int SlotId { get; set; }
        public Slot? Slot { get; set; }

        // Nutzer des Parkvorgangs bzw. Admin bei einer Freigabe
        public int? UserId { get; set; }
        public User? User { get; set; }

        public BoxEventType Type { get; set; }
        public DateTime Timestamp { get; set; }

        // Riegel nach diesem Ereignis offen?
        public static bool OpensLock(BoxEventType type) => type is BoxEventType.Booked or BoxEventType.PickupRequested;
    }
}
