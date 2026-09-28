namespace bikestation.Options
{
    // Wird aus appsettings ("Jwt") geladen. Der Key gehört NICHT ins Repo –
    // auf dem Raspberry Pi per Umgebungsvariable Jwt__Key setzen.
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "bikestation-api";
        public string Audience { get; set; } = "bikestation-dashboard";

        // Mindestens 32 Zeichen (256 Bit) für HMAC-SHA256
        public string Key { get; set; } = string.Empty;

        public int ExpiresMinutes { get; set; } = 60;
    }
}
