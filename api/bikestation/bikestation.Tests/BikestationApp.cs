using System.Net.Http.Headers;
using System.Net.Http.Json;
using bikestation.Data;
using bikestation.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace bikestation.Tests
{
    // Startet die echte API mit einer eigenen, temporären SQLite-Datei pro Test.
    public class BikestationApp : WebApplicationFactory<Program>
    {
        public const string ApiKey = "test-geraete-key-1234567890";
        public const string AdminUser = "testadmin";
        public const string AdminPassword = "test-passwort-123456";

        public string DatabaseFile { get; } = Path.Combine(Path.GetTempPath(), $"bikestation-test-{Guid.NewGuid():N}.db");

        private readonly string _environment;
        private readonly Dictionary<string, string?> _settings;

        public BikestationApp(string environment = "Testing", Dictionary<string, string?>? settings = null)
        {
            _environment = environment;
            _settings = settings ?? [];
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            // UseSetting statt ConfigureAppConfiguration, weil Program.cs die Werte schon vor Build() liest
            builder.UseSetting("ConnectionStrings:Bikestation", $"Data Source={DatabaseFile}");
            builder.UseSetting("Devices:ApiKey", ApiKey);
            builder.UseSetting("Jwt:Key", "test-jwt-schluessel-mindestens-32-zeichen-lang");
            // Hintergrund-Erkennung standardmäßig aus, damit Tests reproduzierbar bleiben
            builder.UseSetting("Anomaly:Enabled", "false");
            foreach (var (key, value) in _settings)
            {
                builder.UseSetting(key, value);
            }
        }

        public HttpClient DeviceClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
            return client;
        }

        public async Task<HttpClient> AdminClientAsync()
        {
            using (var scope = Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<AuthService>().CreateAdminAsync(AdminUser, AdminPassword);
            }

            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new { username = AdminUser, password = AdminPassword });
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResult>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
            return client;
        }

        public T WithDb<T>(Func<BikestationDbContext, T> action)
        {
            using var scope = Services.CreateScope();
            return action(scope.ServiceProvider.GetRequiredService<BikestationDbContext>());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(DatabaseFile) + "*"))
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }

        public record LoginResult(string Token, DateTime ExpiresAt, string Username);
    }
}
