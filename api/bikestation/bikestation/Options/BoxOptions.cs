namespace bikestation.Options
{
    // Wird aus appsettings ("Box") geladen – Werte am echten Modell kalibrieren
    public class BoxOptions
    {
        public const string SectionName = "Box";

        // Anzahl der Stationen beim allerersten Start (leere Datenbank). Danach verwalten Admins die
        // Stationen im Admin-Bereich (hinzufügen/löschen).
        public int StationCount { get; set; } = 1;

        // Ort je Box: erster Eintrag = Box 1 usw. Wird nur für Boxen ohne Ort übernommen.
        public string[] Locations { get; set; } = [];

        // Fahrrad gilt als "da", wenn der Ultraschallsensor höchstens so weit misst (cm)
        public int BikePresentMaxDistanceCm { get; set; } = 7;

        // So lange muss das Fahrrad ununterbrochen erkannt werden, bevor die Box verriegelt
        public int ParkConfirmSeconds { get; set; } = 5;

        // So lange muss das Fahrrad beim Abholen weg sein, bevor die Box wieder frei ist
        public int LeaveConfirmSeconds { get; set; } = 5;

        // Fehlt das Fahrrad so lange in einer verriegelten Box, wird Alarm ausgelöst
        public int AlarmConfirmSeconds { get; set; } = 3;

        // Wird nach dem Öffnen kein Fahrrad eingestellt, wird die Box danach wieder frei
        public int OpenForParkingTimeoutSeconds { get; set; } = 120;

        // Wird das Fahrrad nach dem Öffnen zum Abholen nicht entnommen, wird wieder verriegelt
        public int OpenForPickupTimeoutSeconds { get; set; } = 120;
    }
}
