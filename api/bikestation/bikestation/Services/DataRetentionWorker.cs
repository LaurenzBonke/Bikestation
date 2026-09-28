using bikestation.Data;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Services
{
    // Datensparsamkeit: löscht einmal täglich Messwerte, die älter als Retention:ReadingDays sind.
    // Meldungen (Alerts) bleiben erhalten, damit die Ereignis-Statistik vollständig bleibt.
    public class DataRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DataRetentionWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var days = configuration.GetValue("Retention:ReadingDays", 90);
            if (days <= 0)
            {
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
                    var cutoff = DateTime.UtcNow.AddDays(-days);
                    var deleted = await db.SensorReadings.Where(r => r.Timestamp < cutoff).ExecuteDeleteAsync(stoppingToken);
                    if (deleted > 0)
                    {
                        logger.LogInformation("{Count} Messwerte älter als {Days} Tage gelöscht", deleted, days);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Fehler beim Löschen alter Messwerte");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
