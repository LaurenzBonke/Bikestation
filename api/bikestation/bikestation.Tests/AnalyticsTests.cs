using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using bikestation.Models;
using bikestation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bikestation.Tests
{
    public class AnalyticsTests
    {
        private static void SeedNormalReadings(BikestationApp app, int slotId, int count)
        {
            var random = new Random(3);
            app.WithDb(db =>
            {
                var start = DateTime.UtcNow.AddHours(-count / 60.0);
                for (var i = 0; i < count; i++)
                {
                    var occupied = i / 40 % 2 == 0;
                    db.SensorReadings.Add(new SensorReading
                    {
                        SlotId = slotId,
                        Pressure = occupied ? 850 + random.Next(-20, 21) : 40 + random.Next(-10, 11),
                        Distance = occupied ? 28 + random.Next(-2, 3) : 80 + random.Next(-2, 3),
                        Occupied = occupied,
                        Timestamp = start.AddMinutes(i)
                    });
                }
                return db.SaveChanges();
            });
        }

        [Fact]
        public async Task Hintergrund_Erkennung_meldet_Manipulation_als_KI_Anomalie()
        {
            using var app = new BikestationApp(settings: new() { ["Anomaly:Enabled"] = "true", ["Anomaly:IntervalSeconds"] = "3600" });
            SeedNormalReadings(app, slotId: 2, count: 400);
            var lastId = app.WithDb(db => db.SensorReadings.Max(r => r.Id));

            var device = app.DeviceClient();
            // Fahrrad steht, dann Rütteln mit starkem Druckabfall
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 2, pressure = 850, distance = 28, vibration = false });
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 2, pressure = 560, distance = 41, vibration = true });

            var worker = app.Services.GetRequiredService<AnomalyDetectionWorker>();
            await worker.CheckNewReadingsAsync(lastId);

            var alerts = await device.GetFromJsonAsync<JsonElement[]>("/api/alerts?slotId=2");
            var anomaly = Assert.Single(alerts!, a => a.GetProperty("type").GetString() == "Anomaly");
            Assert.True(anomaly.GetProperty("score").GetDouble() >= 0.6);
            Assert.Contains("Ungewöhnliche Aktivität an Stellplatz 2", anomaly.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Normale_Messungen_erzeugen_keine_KI_Anomalie()
        {
            using var app = new BikestationApp(settings: new() { ["Anomaly:Enabled"] = "true", ["Anomaly:IntervalSeconds"] = "3600" });
            SeedNormalReadings(app, slotId: 1, count: 400);
            var lastId = app.WithDb(db => db.SensorReadings.Max(r => r.Id));

            var device = app.DeviceClient();
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 1, pressure = 845, distance = 28, vibration = false });
            await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 1, pressure = 39, distance = 81, vibration = false });

            await app.Services.GetRequiredService<AnomalyDetectionWorker>().CheckNewReadingsAsync(lastId);

            Assert.Empty((await device.GetFromJsonAsync<JsonElement[]>("/api/alerts"))!);
        }

        [Fact]
        public async Task Prognose_nutzt_historische_Daten()
        {
            using var app = new BikestationApp();
            using (var scope = app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DemoDataService>().GenerateAsync(14);
            }

            var forecast = await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/forecast?hours=6");

            Assert.Equal(3, forecast.GetProperty("totalSlots").GetInt32());
            var hours = forecast.GetProperty("hours").EnumerateArray().ToList();
            Assert.Equal(6, hours.Count);
            Assert.All(hours, h =>
            {
                var free = h.GetProperty("expectedFree").GetDouble();
                Assert.InRange(free, 0, 3);
            });
        }

        [Fact]
        public async Task Prognose_ohne_Daten_liefert_keine_Werte()
        {
            using var app = new BikestationApp();
            var forecast = await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/forecast?hours=3");
            Assert.All(forecast.GetProperty("hours").EnumerateArray(),
                h => Assert.Equal(JsonValueKind.Null, h.GetProperty("expectedFree").ValueKind));
        }

        [Fact]
        public async Task Statistik_enthaelt_Verlauf_pro_Tag()
        {
            using var app = new BikestationApp();
            using (var scope = app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DemoDataService>().GenerateAsync(7);
            }

            var stats = await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/statistics?days=7");
            var days = stats.GetProperty("occupancyByDay").EnumerateArray().ToList();
            Assert.InRange(days.Count, 7, 8);
            Assert.Equal(stats.GetProperty("totalReadings").GetInt32(), days.Sum(d => d.GetProperty("readings").GetInt32()));
        }

        [Fact]
        public async Task Health_meldet_Datenbank_ok()
        {
            using var app = new BikestationApp();
            var health = await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/health");
            Assert.Equal("ok", health.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Fehler_kommen_als_ProblemDetails()
        {
            using var app = new BikestationApp();
            var response = await app.CreateClient().GetAsync("/api/slots/99");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Alte_Messwerte_werden_geloescht()
        {
            using var app = new BikestationApp(settings: new() { ["Retention:ReadingDays"] = "30" });
            app.WithDb(db =>
            {
                db.SensorReadings.Add(new SensorReading { SlotId = 1, Pressure = 40, Distance = 80, Timestamp = DateTime.UtcNow.AddDays(-40) });
                db.SensorReadings.Add(new SensorReading { SlotId = 1, Pressure = 40, Distance = 80, Timestamp = DateTime.UtcNow.AddDays(-1) });
                return db.SaveChanges();
            });

            // Der Hintergrunddienst läuft beim Start einmal sofort – ein neuer Host startet ihn erneut
            using var restarted = app.WithWebHostBuilder(_ => { });
            _ = restarted.CreateClient();
            for (var i = 0; i < 50 && app.WithDb(db => db.SensorReadings.Count()) > 1; i++)
            {
                await Task.Delay(100);
            }

            Assert.Equal(1, app.WithDb(db => db.SensorReadings.Count()));
        }
    }
}
