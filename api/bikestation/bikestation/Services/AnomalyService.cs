using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    public class AnomalyService(
        BikestationDbContext db,
        BoxService boxService,
        IOptions<OccupancyOptions> options,
        ILogger<AnomalyService> logger)
    {
        // Gibt null zurück, wenn der Slot nicht existiert
        public async Task<AnomalyResponse?> ReportAsync(AnomalyRequest request)
        {
            var slot = await db.Slots.FindAsync(request.SlotId!.Value);
            if (slot is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var cooldownStart = now.AddSeconds(-options.Value.AnomalyAlertCooldownSeconds);

            // Keine Flut an Meldungen: solange eine offene, frische Anomalie existiert, nichts Neues anlegen
            var recentExists = await db.Alerts.AnyAsync(a =>
                a.SlotId == slot.Id &&
                a.Type == AlertType.Anomaly &&
                !a.Resolved &&
                a.Timestamp >= cooldownStart);
            if (recentExists)
            {
                return new AnomalyResponse(false, null);
            }

            var score = request.Score!.Value;
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" ({request.Reason.Trim()})";
            var alert = new Alert
            {
                SlotId = slot.Id,
                Type = AlertType.Anomaly,
                Severity = score >= 0.8 ? AlertSeverity.Critical : AlertSeverity.Warning,
                Message = $"Ungewöhnliche Aktivität an {slot.Name}{reason}.",
                Score = Math.Round(score, 2),
                UserId = await boxService.OwnerOfAsync(slot),
                Timestamp = now
            };
            db.Alerts.Add(alert);
            await db.SaveChangesAsync();

            logger.LogWarning("KI-Anomalie an Slot {SlotId}, Score {Score}", slot.Id, score);
            return new AnomalyResponse(true, alert.Id);
        }
    }
}
