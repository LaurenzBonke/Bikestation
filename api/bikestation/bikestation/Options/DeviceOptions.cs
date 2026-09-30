namespace bikestation.Options
{
    // Wird aus appsettings ("Devices") geladen
    public class DeviceOptions
    {
        public const string SectionName = "Devices";

        // Gemeinsamer Schlüssel für die Station (Header "X-Api-Key").
        // Nicht ins Repo – auf dem Raspberry Pi per Umgebungsvariable Devices__ApiKey setzen.
        public string ApiKey { get; set; } = string.Empty;

        // Meldet eine Station länger nicht, gilt sein Stellplatz als offline
        public int OfflineAfterSeconds { get; set; } = 30;
    }
}
