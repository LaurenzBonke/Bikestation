using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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
        public async Task Stationsanzahl_kommt_aus_der_Konfiguration()
        {
            // Erst 3 Boxen mit Messwert an Box 3, dann Neustart mit nur einer Box
            var file = Path.Combine(Path.GetTempPath(), $"bikestation-stationen-{Guid.NewGuid():N}.db");
            var db = $"Data Source={file}";
            using (var app = new BikestationApp(settings: new() { ["ConnectionStrings:Bikestation"] = db }))
            {
                await app.DeviceClient().PostAsJsonAsync("/api/sensor-data", new { slotId = 3, pressure = 0, distance = 80, vibration = false });
                Assert.Equal(3, (await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/slots")).GetArrayLength());
            }
            using (var app = new BikestationApp(settings: new() { ["ConnectionStrings:Bikestation"] = db, ["Box:StationCount"] = "1" }))
            {
                var slots = await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/slots");
                Assert.Equal(1, slots.GetArrayLength());
                Assert.Equal(1, slots[0].GetProperty("id").GetInt32());
                var response = await app.DeviceClient().PostAsJsonAsync("/api/sensor-data", new { slotId = 3, pressure = 0, distance = 80, vibration = false });
                Assert.False(response.IsSuccessStatusCode);
            }
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
        public async Task Veraltete_Datenbank_wird_ohne_Datenverlust_umgestellt()
        {
            // Produktionsmodus wie auf dem Server: dort darf beim Update nichts verloren gehen
            using var app = new BikestationApp("Production");
            var hash = new PasswordHasher<object>().HashPassword(new object(), "altes-admin-passwort");

            // Datenbank im Format der vorherigen Version: AdminUsers statt Users, Slots ohne Box-Spalten
            using (var connection = new SqliteConnection($"Data Source={app.DatabaseFile}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    CREATE TABLE Slots (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Status TEXT NOT NULL, LastUpdated TEXT);
                    CREATE TABLE SensorReadings (Id INTEGER PRIMARY KEY, SlotId INTEGER NOT NULL, Pressure INTEGER NOT NULL,
                        Distance INTEGER NOT NULL, Vibration INTEGER NOT NULL, Occupied INTEGER NOT NULL, Timestamp TEXT NOT NULL);
                    CREATE TABLE Alerts (Id INTEGER PRIMARY KEY, SlotId INTEGER NOT NULL, Type TEXT NOT NULL, Severity TEXT NOT NULL,
                        Message TEXT NOT NULL, Timestamp TEXT NOT NULL, Resolved INTEGER NOT NULL, Score REAL);
                    CREATE TABLE AdminUsers (Id INTEGER PRIMARY KEY, Username TEXT NOT NULL, PasswordHash TEXT NOT NULL, CreatedAt TEXT NOT NULL);
                    INSERT INTO Slots VALUES (1, 'Stellplatz 1', 'Occupied', '2026-09-28 10:00:00'), (2, 'Stellplatz 2', 'Free', NULL), (3, 'Stellplatz 3', 'Free', NULL);
                    INSERT INTO SensorReadings VALUES (1, 1, 850, 28, 0, 1, '2026-09-28 10:00:00'), (2, 2, 40, 80, 0, 0, '2026-09-28 10:00:00');
                    INSERT INTO Alerts VALUES (1, 2, 'PossibleTampering', 'Warning', 'Mögliche Manipulation an Stellplatz 2 erkannt.', '2026-09-28 10:00:00', 0, NULL);
                    INSERT INTO AdminUsers VALUES (1, 'admin', '{hash}', '2026-09-28 09:00:00');
                    """;
                command.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            var client = app.CreateClient();
            var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "altes-admin-passwort" });

            // Admin-Konto übernommen, jetzt mit Rolle Admin
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Equal("Admin", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
            // Messwerte und Meldungen übernommen, Boxen starten als frei
            Assert.Equal(2, app.WithDb(db => db.SensorReadings.Count()));
            Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/alerts"))!);
            var boxes = await client.GetFromJsonAsync<JsonElement[]>("/api/boxes");
            Assert.All(boxes!, b => Assert.Equal("Free", b.GetProperty("state").GetString()));
            // Sicherung der alten Datei liegt daneben
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(app.DatabaseFile)!,
                Path.GetFileName(app.DatabaseFile) + ".vor-update-*"));
        }
    }
}
