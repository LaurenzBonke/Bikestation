using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using bikestation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bikestation.Tests
{
    // Demo-Modus: virtuelle Station statt Raspberry Pi, Demo-Konten und Beispieldaten
    public class DemoTests : IDisposable
    {
        // Wie appsettings.Demo.json, aber mit Wartezeiten 0, damit jede Messrunde sofort schaltet
        private readonly BikestationApp _app = new(settings: new()
        {
            ["Demo:VirtualStation"] = "true",
            ["Demo:HistoryDays"] = "2",
            ["Demo:Accounts:0:Username"] = "demo",
            ["Demo:Accounts:0:Password"] = "bikestation-demo",
            ["Demo:Accounts:0:Role"] = "User",
            ["Demo:Accounts:1:Username"] = "admin",
            ["Demo:Accounts:1:Password"] = "bikestation-admin",
            ["Demo:Accounts:1:Role"] = "Admin",
            ["Box:ParkConfirmSeconds"] = "0",
            ["Box:LeaveConfirmSeconds"] = "0",
            ["Box:AlarmConfirmSeconds"] = "0",
        });

        public void Dispose() => _app.Dispose();

        private VirtualStation Station => _app.Services.GetRequiredService<VirtualStation>();

        private async Task<HttpClient> LoginAsync(string username, string password)
        {
            var client = _app.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());
            return client;
        }

        private async Task<JsonElement> VirtualBox(int slotId)
        {
            await Station.TickAsync();
            var boxes = await _app.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/demo/station");
            return boxes!.Single(b => b.GetProperty("slotId").GetInt32() == slotId);
        }

        private async Task SetBike(int slotId, bool present)
        {
            var response = await _app.CreateClient().PostAsJsonAsync($"/api/demo/station/{slotId}/bike", new { present });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task Demo_Info_nennt_Konten_und_virtuelle_Station()
        {
            var info = await _app.CreateClient().GetFromJsonAsync<JsonElement>("/api/demo");

            Assert.True(info.GetProperty("virtualStation").GetBoolean());
            var accounts = info.GetProperty("accounts").EnumerateArray().ToList();
            Assert.Equal(["demo", "admin"], accounts.Select(a => a.GetProperty("username").GetString()));
            Assert.Equal("Admin", accounts[1].GetProperty("role").GetString());
        }

        [Fact]
        public async Task Demo_Konten_werden_angelegt_und_Admin_hat_Adminrechte()
        {
            var admin = await LoginAsync("admin", "bikestation-admin");
            var user = await LoginAsync("demo", "bikestation-demo");

            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/boxes/occupancy")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/boxes/occupancy")).StatusCode);
        }

        [Fact]
        public void Beispieldaten_werden_beim_Start_erzeugt()
        {
            var count = _app.WithDb(db => db.SensorReadings.Count());
            Assert.True(count > 0, "Beim Start sollten Messwerte der letzten Tage erzeugt werden");
            // Wie an der echten Station: nur Ultraschall, kein Drucksensor
            Assert.Equal(0, _app.WithDb(db => db.SensorReadings.Count(r => r.Pressure != 0)));
        }

        [Fact]
        public async Task Virtuelle_Station_parkt_verriegelt_und_meldet_Diebstahl()
        {
            var user = await LoginAsync("demo", "bikestation-demo");
            var free = await VirtualBox(1);
            Assert.Equal("Free", free.GetProperty("boxState").GetString());
            Assert.True(free.GetProperty("ledGreen").GetBoolean());
            Assert.False(free.GetProperty("lockOpen").GetBoolean());

            // Buchen: Riegel öffnet
            (await user.PostAsync("/api/boxes/1/book", null)).EnsureSuccessStatusCode();
            var open = await VirtualBox(1);
            Assert.Equal("OpenForParking", open.GetProperty("boxState").GetString());
            Assert.True(open.GetProperty("lockOpen").GetBoolean());

            // Fahrrad hineinstellen: Box verriegelt, grüne LED aus
            await SetBike(1, true);
            await Station.TickAsync();
            var locked = await VirtualBox(1);
            Assert.Equal("Locked", locked.GetProperty("boxState").GetString());
            Assert.False(locked.GetProperty("lockOpen").GetBoolean());
            Assert.False(locked.GetProperty("ledGreen").GetBoolean());
            Assert.True(locked.GetProperty("distanceCm").GetInt32() <= 7);

            // Fahrrad ohne Öffnen herausnehmen: Box gesperrt, Alarm für den Besitzer
            await SetBike(1, false);
            await Station.TickAsync();
            var blocked = await VirtualBox(1);
            Assert.Equal("Blocked", blocked.GetProperty("boxState").GetString());
            var me = await user.GetFromJsonAsync<JsonElement>("/api/me");
            Assert.Contains(me.GetProperty("alerts").EnumerateArray(), a => a.GetProperty("type").GetString() == "BikeRemoved");
        }

        [Fact]
        public async Task Unbekannte_Box_der_virtuellen_Station_gibt_404()
        {
            await Station.TickAsync();
            var response = await _app.CreateClient().PostAsJsonAsync("/api/demo/station/99/bike", new { present = true });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Ohne_Demo_Konfiguration_ist_alles_aus()
        {
            using var app = new BikestationApp();
            var client = app.CreateClient();

            var info = await client.GetFromJsonAsync<JsonElement>("/api/demo");
            Assert.False(info.GetProperty("virtualStation").GetBoolean());
            Assert.Empty(info.GetProperty("accounts").EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/demo/station")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsJsonAsync("/api/demo/station/1/bike", new { present = true })).StatusCode);
            Assert.Null(app.Services.GetService<VirtualStation>());
            Assert.Equal(0, app.WithDb(db => db.Users.Count()));
            Assert.Equal(0, app.WithDb(db => db.SensorReadings.Count()));
        }
    }
}
