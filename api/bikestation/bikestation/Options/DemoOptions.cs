namespace bikestation.Options
{
    // Wird aus appsettings.json ("Demo") geladen. Alles ist standardmäßig aus –
    // eingeschaltet wird es nur in appsettings.Demo.json (ASPNETCORE_ENVIRONMENT=Demo).
    public class DemoOptions
    {
        public const string SectionName = "Demo";

        // Erlaubt den Button "Demo-Daten erzeugen" im Admin-Bereich auch außerhalb von Development
        public bool Enabled { get; set; }

        // Virtuelle Station statt Raspberry Pi: meldet für jede Box Messwerte, das Fahrrad wird im Browser hineingestellt
        public bool VirtualStation { get; set; }

        // Beim ersten Start Messwerte der letzten Tage erzeugen, damit Statistik und Prognose etwas zeigen (0 = aus)
        public int HistoryDays { get; set; }

        // Konten, die beim Start angelegt werden. Werden auf der Anmeldeseite angezeigt – nur für die Demo!
        public List<DemoAccount> Accounts { get; set; } = [];
    }

    public class DemoAccount
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }
}
