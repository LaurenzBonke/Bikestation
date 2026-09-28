using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace bikestation.Tests
{
    public class StatisticsAndServerTests
    {
        [Fact]
        public async Task Statistik_zaehlt_Auslastung_und_Ereignisse()
        {
            using var app = new BikestationApp();
            var device = app.DeviceClient();
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 1, pressure = 800, distance = 28, vibration = false });
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 1, pressure = 800, distance = 28, vibration = true });
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 2, pressure = 40, distance = 80, vibration = false });
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 3, pressure = 40, distance = 80, vibration = false });

            var stats = await device.GetFromJsonAsync<JsonElement>("/api/statistics?days=7");

            Assert.Equal(4, stats.GetProperty("totalReadings").GetInt32());
            Assert.Equal(50.0, stats.GetProperty("occupancyPercent").GetDouble());
            Assert.Equal(1, stats.GetProperty("tamperEvents").GetInt32());
            Assert.Equal(24, stats.GetProperty("occupancyByHour").GetArrayLength());

            var slot1 = stats.GetProperty("slots")[0];
            Assert.Equal(100.0, slot1.GetProperty("occupancyPercent").GetDouble());
            Assert.Equal(1, slot1.GetProperty("tamperEvents").GetInt32());
        }

        [Fact]
        public async Task Demo_Daten_sind_im_Serverbetrieb_standardmaessig_aus()
        {
            using var app = new BikestationApp("Production");
            var admin = await app.AdminClientAsync();
            var response = await admin.PostAsync("/api/demo/generate?days=1", null);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Demo_Daten_lassen_sich_per_Schalter_einschalten()
        {
            using var app = new BikestationApp("Production", new() { ["Demo:Enabled"] = "true" });
            var admin = await app.AdminClientAsync();

            var response = await admin.PostAsync("/api/demo/generate?days=1", null);
            response.EnsureSuccessStatusCode();
            var readings = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("readings").GetInt32();
            Assert.Equal(3 * 24 * 12, readings);  // 3 Plätze, 24 h, alle 5 Minuten
        }

        [Fact]
        public async Task Antworten_haben_Sicherheits_Header()
        {
            using var app = new BikestationApp();
            var response = await app.CreateClient().GetAsync("/api/slots");
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        }

        [Fact]
        public async Task Veraltete_Datenbank_wird_gesichert_und_neu_angelegt()
        {
            using var app = new BikestationApp("Development");

            // Datenbank im Format der allerersten Version anlegen (ohne AdminUsers, ohne Alerts.Score)
            using (var connection = new SqliteConnection($"Data Source={app.DatabaseFile}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Slots (Id INTEGER PRIMARY KEY, Name TEXT, Status TEXT, LastUpdated TEXT);
                    CREATE TABLE Alerts (Id INTEGER PRIMARY KEY, SlotId INTEGER, Type TEXT, Severity TEXT,
                                         Message TEXT, Timestamp TEXT, Resolved INTEGER);
                    """;
                command.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            var response = await app.CreateClient().GetAsync("/api/alerts");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(app.DatabaseFile)!,
                Path.GetFileName(app.DatabaseFile) + ".veraltet-*"));
        }
    }
}
