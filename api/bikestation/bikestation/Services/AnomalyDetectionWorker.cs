using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    // Läuft im Hintergrund der API: prüft neue Messwerte regelmäßig mit dem AnomalyDetector
    // und legt bei Auffälligkeiten eine Meldung "KI-Anomalie" an.
    public class AnomalyDetectionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AnomalyOptions> options,
        ILogger<AnomalyDetectionWorker> logger) : BackgroundService
    {
        private readonly AnomalyOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                logger.LogInformation("Eingebaute Anomalieerkennung ist ausgeschaltet");
                return;
            }

            // Nur Messwerte ab dem Start prüfen, alte Daten dienen als Lerngrundlage
            long lastId;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
                lastId = await db.SensorReadings.MaxAsync(r => (int?)r.Id, stoppingToken) ?? 0;
            }
            logger.LogInformation("Anomalieerkennung aktiv (Z-Schwelle {Threshold})", _options.ZThreshold);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    lastId = await CheckNewReadingsAsync(lastId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Fehler in der Anomalieerkennung");
                }
            }
        }

        // Gibt die ID des zuletzt geprüften Messwerts zurück
        public async Task<long> CheckNewReadingsAsync(long lastId, CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
            var anomalyService = scope.ServiceProvider.GetRequiredService<AnomalyService>();

            var newReadings = await db.SensorReadings.AsNoTracking()
                .Where(r => r.Id > lastId)
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken);
            if (newReadings.Count == 0)
            {
                return lastId;
            }

            foreach (var slotReadings in newReadings.GroupBy(r => r.SlotId))
            {
                var slotId = slotReadings.Key;
                var history = await db.SensorReadings.AsNoTracking()
                    .Where(r => r.SlotId == slotId && r.Id <= lastId)
                    .OrderByDescending(r => r.Id)
                    .Take(_options.HistorySize)
                    .ToListAsync(cancellationToken);
                history.Reverse();

                var detector = new AnomalyDetector(_options.ZThreshold, _options.MinSamples);
                detector.Train(slotId, history);

                SensorReading? previous = history.LastOrDefault();
                foreach (var reading in slotReadings)
                {
                    var result = detector.Evaluate(reading, previous);
                    if (result.IsAnomaly)
                    {
                        logger.LogWarning("Anomalie an Slot {SlotId}: Z={Z}, Score={Score} ({Reason})",
                            slotId, result.MaxZ, result.Score, result.Reason);
                        await anomalyService.ReportAsync(new AnomalyRequest
                        {
                            SlotId = slotId,
                            Score = result.Score,
                            Reason = result.Reason
                        });
                    }
                    previous = reading;
                }
            }

            return newReadings[^1].Id;
        }
    }
}
