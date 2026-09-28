namespace bikestation.Options
{
    // Wird aus appsettings ("Anomaly") geladen
    public class AnomalyOptions
    {
        public const string SectionName = "Anomaly";

        // Eingebaute Erkennung im Backend an/aus (der Python-Dienst in ai/ kann zusätzlich laufen)
        public bool Enabled { get; set; } = true;

        // So oft werden neue Messwerte geprüft
        public int IntervalSeconds { get; set; } = 10;

        // Ab dieser Abweichung (robuster Z-Score) gilt ein Messwert als ungewöhnlich
        public double ZThreshold { get; set; } = 6;

        // Aus so vielen früheren Messwerten pro Stellplatz wird "normal" gelernt
        public int HistorySize { get; set; } = 2000;

        // Mindestens so viele Messwerte pro Zustand (frei/belegt), bevor bewertet wird
        public int MinSamples { get; set; } = 30;
    }
}
