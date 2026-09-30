using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    // Bereitet beim Start die Demo vor: Demo-Konten anlegen und – bei leerer Datenbank – Messwerte der
    // letzten Tage erzeugen. Läuft vor den Hintergrunddiensten, damit die KI die erzeugten Werte als
    // Lerngrundlage nimmt und nicht als neue Messungen prüft.
    public static class DemoSetup
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            var options = services.GetRequiredService<IOptions<DemoOptions>>().Value;
            if (options.Accounts.Count == 0 && options.HistoryDays <= 0)
            {
                return;
            }

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
            var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("bikestation.Demo");

            foreach (var account in options.Accounts)
            {
                if (await db.Users.AnyAsync(u => u.Username == account.Username)) continue;

                if (Enum.TryParse<UserRole>(account.Role, ignoreCase: true, out var role) && role == UserRole.Admin)
                {
                    await auth.CreateAdminAsync(account.Username, account.Password);
                }
                else
                {
                    await auth.RegisterAsync(new RegisterRequest { Username = account.Username, Password = account.Password });
                }
                logger.LogInformation("Demo: Konto {Username} angelegt", account.Username);
            }

            if (options.HistoryDays > 0 && !await db.SensorReadings.AnyAsync())
            {
                var demoData = scope.ServiceProvider.GetRequiredService<DemoDataService>();
                var count = await demoData.GenerateAsync(options.HistoryDays);
                logger.LogInformation("Demo: {Count} Messwerte der letzten {Days} Tage erzeugt", count, options.HistoryDays);
            }
        }
    }
}
