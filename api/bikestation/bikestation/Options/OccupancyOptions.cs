namespace bikestation.Options
{
    // Wird aus appsettings.json ("Occupancy") geladen
    public class OccupancyOptions
    {
        public const string SectionName = "Occupancy";

        // ANNAHME: Startwert, muss mit dem echten SEN0616 kalibriert werden
        public int PressureThreshold { get; set; } = 500;

        // Innerhalb dieses Zeitraums wird kein zweiter Manipulations-Alert für denselben Slot erzeugt
        public int TamperAlertCooldownSeconds { get; set; } = 60;

        // Dasselbe für KI-Anomalien
        public int AnomalyAlertCooldownSeconds { get; set; } = 300;
    }
}
