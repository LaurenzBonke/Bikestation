using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using bikestation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bikestation.Tests
{
    // Konten, Buchen, Riegel, Abholen und Alarm der abschließbaren Boxen
    public class BoxTests : IAsyncLifetime, IDisposable
    {
        private const int Near = 3;   // Fahrrad direkt vor dem Sensor (<= 5 cm)
        private const int Far = 80;   // nichts in der Box

        // Wartezeiten 0: jede Messung schaltet sofort (eigene Tests prüfen die Wartezeiten)
        private readonly BikestationApp _app = new(settings: new()
        {
            ["Box:ParkConfirmSeconds"] = "0",
            ["Box:LeaveConfirmSeconds"] = "0",
            ["Box:AlarmConfirmSeconds"] = "0",
        });

        public void Dispose() => _app.Dispose();

        // Station ist verbunden: jede Box hat gerade gemeldet (sonst lehnt die API das Buchen ab)
        public async Task InitializeAsync()
        {
            var device = _app.DeviceClient();
            foreach (var slot in new[] { 1, 2, 3 }) await Reading(device, slot, Far);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<HttpClient> RegisterAsync(BikestationApp app, string username)
        {
            var client = app.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "sicheres-passwort" });
            response.EnsureSuccessStatusCode();
            var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static Task<HttpResponseMessage> Reading(HttpClient device, int slotId, int distance, bool vibration = false) =>
            device.PostAsJsonAsync("/api/sensor-data", new { slotId, pressure = 0, distance, vibration });

        private static async Task<JsonElement> Box(HttpClient client, int id) =>
            (await client.GetFromJsonAsync<JsonElement[]>("/api/boxes"))!.Single(b => b.GetProperty("id").GetInt32() == id);

        private static async Task<JsonElement> Me(HttpClient client) => await client.GetFromJsonAsync<JsonElement>("/api/me");

        [Fact]
        public async Task Registrieren_liefert_Nutzer_Token()
        {
            var response = await _app.CreateClient().PostAsJsonAsync("/api/auth/register", new { username = "anna", password = "sicheres-passwort" });
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("User", body.GetProperty("role").GetString());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        }

        [Fact]
        public async Task Nutzer_bleiben_laenger_angemeldet_als_Admins()
        {
            var user = await _app.CreateClient().PostAsJsonAsync("/api/auth/register", new { username = "anna", password = "sicheres-passwort" });
            var userExpires = (await user.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expiresAt").GetDateTime();
            await _app.AdminClientAsync();
            var admin = await _app.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { username = BikestationApp.AdminUser, password = BikestationApp.AdminPassword });
            var adminExpires = (await admin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expiresAt").GetDateTime();

            Assert.True(userExpires > DateTime.UtcNow.AddHours(11), "Nutzer: ca. 12 Stunden");
            Assert.True(adminExpires < DateTime.UtcNow.AddMinutes(61), "Admin: 60 Minuten");
        }

        [Theory]
        [InlineData("ab", "sicheres-passwort")]          // Name zu kurz
        [InlineData("anna mit leerzeichen", "sicheres-passwort")]
        [InlineData("anna", "kurz")]                      // Passwort zu kurz
        public async Task Ungueltige_Registrierung_liefert_400(string username, string password)
        {
            var response = await _app.CreateClient().PostAsJsonAsync("/api/auth/register", new { username, password });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Benutzername_ist_eindeutig_ohne_Gross_Kleinschreibung()
        {
            await RegisterAsync(_app, "anna");
            var response = await _app.CreateClient().PostAsJsonAsync("/api/auth/register", new { username = "ANNA", password = "sicheres-passwort" });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Nutzer_kann_keine_Admin_Funktionen()
        {
            var user = await RegisterAsync(_app, "anna");
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync("/api/alerts/1/resolve", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync("/api/boxes/1/release", null)).StatusCode);
        }

        [Fact]
        public async Task Buchen_ohne_Anmeldung_geht_nicht()
        {
            var response = await _app.CreateClient().PostAsync("/api/boxes/1/book", null);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Kompletter_Ablauf_Buchen_Parken_Abholen()
        {
            var user = await RegisterAsync(_app, "anna");
            var device = _app.DeviceClient();

            // 1. Buchen: Riegel öffnet
            Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("/api/boxes/1/book", null)).StatusCode);
            var box = await Box(user, 1);
            Assert.Equal("OpenForParking", box.GetProperty("state").GetString());
            Assert.True(box.GetProperty("lockOpen").GetBoolean());
            Assert.True(box.GetProperty("isMine").GetBoolean());
            var deviceView = (await device.GetFromJsonAsync<JsonElement[]>("/api/device/boxes"))!.Single(b => b.GetProperty("slotId").GetInt32() == 1);
            Assert.True(deviceView.GetProperty("lockOpen").GetBoolean());

            // 2. Fahrrad eingestellt: Riegel schließt – das Gerät erfährt es direkt aus der Antwort
            var parked = await (await Reading(device, 1, Near)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Locked", parked.GetProperty("boxState").GetString());
            Assert.False(parked.GetProperty("lockOpen").GetBoolean());
            Assert.Equal("Locked", (await Me(user)).GetProperty("parking").GetProperty("state").GetString());

            // 3. Abholen: Riegel öffnet
            Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("/api/boxes/1/pickup", null)).StatusCode);
            Assert.True((await Box(user, 1)).GetProperty("lockOpen").GetBoolean());

            // 4. Fahrrad entnommen: Box wieder frei, Parkvorgang abgeschlossen
            var left = await (await Reading(device, 1, Far)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Free", left.GetProperty("boxState").GetString());
            var me = await Me(user);
            Assert.Equal(JsonValueKind.Null, me.GetProperty("parking").ValueKind);
            Assert.Equal("Completed", me.GetProperty("history")[0].GetProperty("endReason").GetString());
            Assert.Empty(me.GetProperty("alerts").EnumerateArray());
        }

        [Fact]
        public async Task Buchen_bei_offline_Station_wird_abgelehnt()
        {
            using var app = new BikestationApp(settings: new() { ["Devices:OfflineAfterSeconds"] = "30" });
            var user = await RegisterAsync(app, "anna");   // Station hat sich noch nie gemeldet
            var response = await user.PostAsync("/api/boxes/1/book", null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("Offline", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
            Assert.Equal("Free", (await Box(user, 1)).GetProperty("state").GetString());
        }

        [Fact]
        public async Task Nur_eine_Box_pro_Nutzer()
        {
            var user = await RegisterAsync(_app, "anna");
            await user.PostAsync("/api/boxes/1/book", null);
            var second = await user.PostAsync("/api/boxes/2/book", null);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal("AlreadyHasBox", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        }

        [Fact]
        public async Task Fremde_Box_kann_nicht_geoeffnet_oder_gebucht_werden()
        {
            var anna = await RegisterAsync(_app, "anna");
            var ben = await RegisterAsync(_app, "ben");
            await anna.PostAsync("/api/boxes/1/book", null);
            await Reading(_app.DeviceClient(), 1, Near);

            Assert.Equal(HttpStatusCode.Forbidden, (await ben.PostAsync("/api/boxes/1/pickup", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await ben.PostAsync("/api/boxes/1/book", null)).StatusCode);
            Assert.False((await Box(ben, 1)).GetProperty("isMine").GetBoolean());
        }

        [Fact]
        public async Task Fahrrad_ohne_Oeffnen_entfernt_loest_Alarm_aus()
        {
            var user = await RegisterAsync(_app, "anna");
            var device = _app.DeviceClient();
            await user.PostAsync("/api/boxes/2/book", null);
            await Reading(device, 2, Near);

            // Fahrrad verschwindet, obwohl die Box verriegelt ist
            var removed = await (await Reading(device, 2, Far)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Blocked", removed.GetProperty("boxState").GetString());
            Assert.False(removed.GetProperty("lockOpen").GetBoolean());

            // Nutzer sieht den Alarm in der App
            var me = await Me(user);
            var alert = Assert.Single(me.GetProperty("alerts").EnumerateArray());
            Assert.Equal("BikeRemoved", alert.GetProperty("type").GetString());
            Assert.Equal("Critical", alert.GetProperty("severity").GetString());
            Assert.Equal("BikeRemoved", me.GetProperty("history")[0].GetProperty("endReason").GetString());

            // Admin sieht ihn ebenfalls
            var admin = await _app.AdminClientAsync();
            Assert.Contains((await admin.GetFromJsonAsync<JsonElement[]>("/api/alerts"))!, a => a.GetProperty("type").GetString() == "BikeRemoved");

            // Nutzer bestätigt -> verschwindet aus seiner App
            Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync($"/api/me/alerts/{alert.GetProperty("id").GetInt32()}/ack", null)).StatusCode);
            Assert.Empty((await Me(user)).GetProperty("alerts").EnumerateArray());

            // Gesperrte Box kann erst nach Freigabe durch den Admin wieder gebucht werden
            Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsync("/api/boxes/2/book", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/boxes/2/release", null)).StatusCode);
            Assert.Equal("Free", (await Box(user, 2)).GetProperty("state").GetString());

            // Protokoll zeigt den ganzen Ablauf mit Nutzer (neueste zuerst), nur für Admins
            var events = (await admin.GetFromJsonAsync<JsonElement[]>("/api/boxes/events?slotId=2"))!;
            Assert.Equal(["Released", "BikeRemoved", "Parked", "Booked"], events.Select(e => e.GetProperty("type").GetString()));
            Assert.Equal(BikestationApp.AdminUser, events[0].GetProperty("username").GetString());
            Assert.All(events[1..], e => Assert.Equal("anna", e.GetProperty("username").GetString()));
            Assert.True(events[3].GetProperty("lockOpen").GetBoolean());
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/boxes/events")).StatusCode);
        }

        [Fact]
        public async Task Admin_sieht_welcher_Nutzer_an_welcher_Box_ist()
        {
            var user = await RegisterAsync(_app, "anna");
            var device = _app.DeviceClient();
            await Reading(device, 1, Far);
            await user.PostAsync("/api/boxes/1/book", null);
            await Reading(device, 1, Near);

            var admin = await _app.AdminClientAsync();
            var boxes = (await admin.GetFromJsonAsync<JsonElement[]>("/api/boxes/occupancy"))!;
            var box1 = boxes.Single(b => b.GetProperty("id").GetInt32() == 1);
            Assert.Equal("anna", box1.GetProperty("username").GetString());
            Assert.Equal("Locked", box1.GetProperty("state").GetString());
            Assert.NotEqual(JsonValueKind.Null, box1.GetProperty("parkedAt").ValueKind);
            Assert.Equal(JsonValueKind.Null, boxes.Single(b => b.GetProperty("id").GetInt32() == 2).GetProperty("username").ValueKind);

            // Normale Nutzer sehen keine fremden Namen
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/boxes/occupancy")).StatusCode);
        }

        [Fact]
        public async Task Admin_kann_Stationen_anlegen_aendern_und_loeschen()
        {
            var admin = await _app.AdminClientAsync();
            var created = await admin.PostAsJsonAsync("/api/boxes", new { location = "Fahrradkeller" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            Assert.Equal(4, id);  // nächste freie Nummer nach den 3 Test-Boxen

            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/boxes/{id}", new { location = "Mensa" })).StatusCode);
            Assert.Equal("Mensa", (await Box(admin, id)).GetProperty("location").GetString());

            // Leerer Ort wird abgelehnt
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/boxes", new { location = "" })).StatusCode);

            // Belegte Station kann nicht gelöscht werden
            var user = await RegisterAsync(_app, "anna");
            var device = _app.DeviceClient();
            await Reading(device, id, Far);
            await user.PostAsync($"/api/boxes/{id}/book", null);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/boxes/{id}")).StatusCode);
            await user.PostAsync($"/api/boxes/{id}/cancel", null);

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/boxes/{id}")).StatusCode);
            var boxes = (await admin.GetFromJsonAsync<JsonElement[]>("/api/boxes"))!;
            Assert.DoesNotContain(boxes, b => b.GetProperty("id").GetInt32() == id);

            // Nur Admins
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/boxes", new { location = "X" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.DeleteAsync("/api/boxes/1")).StatusCode);
        }

        [Fact]
        public async Task Fremde_Meldung_kann_nicht_bestaetigt_werden()
        {
            var anna = await RegisterAsync(_app, "anna");
            var ben = await RegisterAsync(_app, "ben");
            await anna.PostAsync("/api/boxes/2/book", null);
            await Reading(_app.DeviceClient(), 2, Near);
            await Reading(_app.DeviceClient(), 2, Far);
            var alertId = (await Me(anna)).GetProperty("alerts")[0].GetProperty("id").GetInt32();

            Assert.Equal(HttpStatusCode.NotFound, (await ben.PostAsync($"/api/me/alerts/{alertId}/ack", null)).StatusCode);
        }

        [Fact]
        public async Task Vibration_an_belegter_Box_meldet_auch_dem_Besitzer()
        {
            var user = await RegisterAsync(_app, "anna");
            var device = _app.DeviceClient();
            await user.PostAsync("/api/boxes/3/book", null);
            await Reading(device, 3, Near);
            await Reading(device, 3, Near, vibration: true);

            var alert = Assert.Single((await Me(user)).GetProperty("alerts").EnumerateArray());
            Assert.Equal("PossibleTampering", alert.GetProperty("type").GetString());
            Assert.Equal("Locked", (await Box(user, 3)).GetProperty("state").GetString());
        }

        [Fact]
        public async Task Buchung_abbrechen_gibt_Box_frei()
        {
            var user = await RegisterAsync(_app, "anna");
            await user.PostAsync("/api/boxes/1/book", null);
            Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("/api/boxes/1/cancel", null)).StatusCode);
            Assert.Equal("Free", (await Box(user, 1)).GetProperty("state").GetString());
            Assert.Equal("Cancelled", (await Me(user)).GetProperty("history")[0].GetProperty("endReason").GetString());
        }

        [Fact]
        public async Task Ohne_Fahrrad_wird_die_Box_nach_Zeitlimit_frei()
        {
            var user = await RegisterAsync(_app, "anna");
            await user.PostAsync("/api/boxes/1/book", null);

            using (var scope = _app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<BoxService>().CheckTimeoutsAsync(DateTime.UtcNow.AddMinutes(5));
            }

            Assert.Equal("Free", (await Box(user, 1)).GetProperty("state").GetString());
            Assert.Equal("TimedOut", (await Me(user)).GetProperty("history")[0].GetProperty("endReason").GetString());
        }

        [Fact]
        public async Task Nicht_abgeholtes_Fahrrad_wird_wieder_verriegelt()
        {
            var user = await RegisterAsync(_app, "anna");
            await user.PostAsync("/api/boxes/1/book", null);
            await Reading(_app.DeviceClient(), 1, Near);
            await user.PostAsync("/api/boxes/1/pickup", null);

            using (var scope = _app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<BoxService>().CheckTimeoutsAsync(DateTime.UtcNow.AddMinutes(5));
            }

            Assert.Equal("Locked", (await Box(user, 1)).GetProperty("state").GetString());
        }

        [Fact]
        public async Task Kurzer_Messausreisser_verriegelt_nicht_sofort()
        {
            // Mit echter Wartezeit: eine einzelne Messung reicht nicht zum Verriegeln
            using var app = new BikestationApp(settings: new() { ["Box:ParkConfirmSeconds"] = "5" });
            var user = await RegisterAsync(app, "anna");
            var device = app.DeviceClient();

            await Reading(device, 1, Near);               // stand schon vor dem Buchen etwas vor dem Sensor
            await user.PostAsync("/api/boxes/1/book", null);
            await Reading(device, 1, Near);

            Assert.Equal("OpenForParking", (await Box(user, 1)).GetProperty("state").GetString());
        }
    }
}
