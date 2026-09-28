using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace bikestation.Tests
{
    public class AuthAndAlertTests : IDisposable
    {
        private readonly BikestationApp _app = new();

        public void Dispose() => _app.Dispose();

        [Fact]
        public async Task Login_mit_falschem_Passwort_liefert_401()
        {
            await _app.AdminClientAsync();
            var response = await _app.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { username = BikestationApp.AdminUser, password = "falsches-passwort-1" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_liefert_gueltigen_Token()
        {
            var admin = await _app.AdminClientAsync();
            var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(BikestationApp.AdminUser, me.GetProperty("username").GetString());
        }

        [Fact]
        public async Task Manipulierter_Token_wird_abgelehnt()
        {
            var client = _app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");
            var response = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Nach_5_Fehlversuchen_greift_das_Rate_Limit()
        {
            var client = _app.CreateClient();
            var codes = new List<HttpStatusCode>();
            for (var i = 0; i < 6; i++)
            {
                var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "x", password = "y" });
                codes.Add(response.StatusCode);
            }
            Assert.All(codes.Take(5), code => Assert.Equal(HttpStatusCode.Unauthorized, code));
            Assert.Equal(HttpStatusCode.TooManyRequests, codes[5]);
        }

        [Fact]
        public async Task Meldung_erledigen_nur_mit_Token()
        {
            var device = _app.DeviceClient();
            (await device.PostAsJsonAsync("/api/sensor-data", new { slotId = 1, pressure = 800, distance = 28, vibration = true }))
                .EnsureSuccessStatusCode();
            var alertId = (await device.GetFromJsonAsync<JsonElement[]>("/api/alerts"))![0].GetProperty("id").GetInt32();

            var anonymous = await _app.CreateClient().PostAsync($"/api/alerts/{alertId}/resolve", null);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            var admin = await _app.AdminClientAsync();
            var resolved = await admin.PostAsync($"/api/alerts/{alertId}/resolve", null);
            Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);
            Assert.Empty((await device.GetFromJsonAsync<JsonElement[]>("/api/alerts"))!);
        }

        [Fact]
        public async Task KI_Anomalie_wird_mit_Score_gespeichert_und_gedrosselt()
        {
            var ai = _app.DeviceClient();
            var first = await ai.PostAsJsonAsync("/api/anomalies", new { slotId = 2, score = 0.85, reason = "Vibration + starke Druckänderung" });
            var second = await ai.PostAsJsonAsync("/api/anomalies", new { slotId = 2, score = 0.9 });

            Assert.True((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetBoolean());
            Assert.False((await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetBoolean());

            var alert = Assert.Single((await ai.GetFromJsonAsync<JsonElement[]>("/api/alerts"))!);
            Assert.Equal("Anomaly", alert.GetProperty("type").GetString());
            Assert.Equal("Critical", alert.GetProperty("severity").GetString());
            Assert.Equal(0.85, alert.GetProperty("score").GetDouble());
            Assert.Contains("Druckänderung", alert.GetProperty("message").GetString());
        }

        [Fact]
        public async Task KI_Anomalie_ohne_ApiKey_wird_abgelehnt()
        {
            var response = await _app.CreateClient().PostAsJsonAsync("/api/anomalies", new { slotId = 2, score = 0.9 });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
