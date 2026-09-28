using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using bikestation.Models;

namespace bikestation.Tests
{
    public class SensorDataTests : IDisposable
    {
        private readonly BikestationApp _app = new();

        public void Dispose() => _app.Dispose();

        private static object Reading(int slotId = 1, int pressure = 800, int distance = 28, bool vibration = false) =>
            new { slotId, pressure, distance, vibration };

        [Fact]
        public async Task Ohne_ApiKey_wird_abgelehnt()
        {
            var response = await _app.CreateClient().PostAsJsonAsync("/api/sensor-data", Reading());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Falscher_ApiKey_wird_abgelehnt()
        {
            var client = _app.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", "falscher-key");
            var response = await client.PostAsJsonAsync("/api/sensor-data", Reading());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(1, 5000, 28)]   // Druck über 12-Bit-ADC
        [InlineData(1, 800, -1)]    // negativer Abstand
        [InlineData(0, 800, 28)]    // Slot-ID 0
        public async Task Ungueltige_Werte_liefern_400(int slotId, int pressure, int distance)
        {
            var response = await _app.DeviceClient().PostAsJsonAsync("/api/sensor-data", Reading(slotId, pressure, distance));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Fehlende_Felder_liefern_400()
        {
            var response = await _app.DeviceClient().PostAsJsonAsync("/api/sensor-data", new { slotId = 1, pressure = 800 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Unbekannter_Slot_liefert_404()
        {
            var response = await _app.DeviceClient().PostAsJsonAsync("/api/sensor-data", Reading(slotId: 99));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData(800, "Occupied", "Belegt")]
        [InlineData(40, "Free", "Frei")]
        public async Task Druck_bestimmt_Belegung(int pressure, string status, string statusText)
        {
            var client = _app.DeviceClient();
            (await client.PostAsJsonAsync("/api/sensor-data", Reading(slotId: 2, pressure: pressure))).EnsureSuccessStatusCode();

            var slot = await client.GetFromJsonAsync<JsonElement>("/api/slots/2");
            Assert.Equal(status, slot.GetProperty("status").GetString());
            Assert.Equal(statusText, slot.GetProperty("statusText").GetString());
            Assert.True(slot.GetProperty("isOnline").GetBoolean());
            Assert.Equal(pressure, slot.GetProperty("latestReading").GetProperty("pressure").GetInt32());
        }

        [Fact]
        public async Task Vibration_erzeugt_genau_eine_Meldung_innerhalb_des_Cooldowns()
        {
            var client = _app.DeviceClient();
            for (var i = 0; i < 3; i++)
            {
                (await client.PostAsJsonAsync("/api/sensor-data", Reading(slotId: 3, vibration: true))).EnsureSuccessStatusCode();
            }

            var alerts = await client.GetFromJsonAsync<JsonElement[]>("/api/alerts");
            var alert = Assert.Single(alerts!);
            Assert.Equal("PossibleTampering", alert.GetProperty("type").GetString());
            Assert.Contains("Mögliche Manipulation", alert.GetProperty("message").GetString());

            var slot = await client.GetFromJsonAsync<JsonElement>("/api/slots/3");
            Assert.True(slot.GetProperty("possibleTampering").GetBoolean());
        }

        [Fact]
        public async Task Slot_ohne_aktuelle_Meldung_ist_offline()
        {
            var client = _app.DeviceClient();
            (await client.PostAsJsonAsync("/api/sensor-data", Reading(slotId: 1))).EnsureSuccessStatusCode();

            // Letzte Meldung künstlich 5 Minuten in die Vergangenheit legen
            _app.WithDb(db =>
            {
                db.Slots.Find(1)!.LastUpdated = DateTime.UtcNow.AddMinutes(-5);
                return db.SaveChanges();
            });

            var slot = await client.GetFromJsonAsync<JsonElement>("/api/slots/1");
            Assert.False(slot.GetProperty("isOnline").GetBoolean());
        }

        [Fact]
        public async Task Messwerte_werden_gespeichert_und_sind_abrufbar()
        {
            var client = _app.DeviceClient();
            (await client.PostAsJsonAsync("/api/sensor-data", Reading(slotId: 1, pressure: 700))).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync("/api/sensor-data", Reading(slotId: 2, pressure: 50))).EnsureSuccessStatusCode();

            var readings = await client.GetFromJsonAsync<JsonElement[]>("/api/sensor-readings?afterId=0");
            Assert.Equal(2, readings!.Length);
            Assert.Equal(1, _app.WithDb(db => db.SensorReadings.Count(r => r.SlotId == 1)));

            // Zeitstempel kommen als UTC ("Z") zurück
            Assert.EndsWith("Z", readings[0].GetProperty("timestamp").GetString());
        }
    }
}
