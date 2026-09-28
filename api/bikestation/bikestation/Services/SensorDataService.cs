using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    public class SensorDataService(
        BikestationDbContext db,
        IOptions<OccupancyOptions> options,
        ILogger<SensorDataService> logger)
    {
        private readonly OccupancyOptions _options = options.Value;

        // Gibt null zurück, wenn der Slot nicht existiert
        public async Task<SensorDataResponse?> ProcessAsync(SensorDataRequest request)
        {
            var slot = await db.Slots.FindAsync(request.SlotId!.Value);
            if (slot is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;

            // Drucksensor ist der primäre Belegungssensor
            var occupied = request.Pressure!.Value >= _options.PressureThreshold;

            db.SensorReadings.Add(new SensorReading
            {
                SlotId = slot.Id,
                Pressure = request.Pressure.Value,
                Distance = request.Distance!.Value,
                Vibration = request.Vibration!.Value,
                Occupied = occupied,
                Timestamp = now
            });

            var newStatus = occupied ? SlotStatus.Occupied : SlotStatus.Free;
            if (slot.Status != newStatus)
            {
                logger.LogInformation("Slot {SlotId}: {Old} -> {New}", slot.Id, slot.Status, newStatus);
            }
            slot.Status = newStatus;
            slot.LastUpdated = now;

            if (request.Vibration.Value)
            {
                await AddTamperAlertIfNeededAsync(slot, now);
            }

            await db.SaveChangesAsync();

            return new SensorDataResponse(slot.Id, occupied, slot.Status);
        }

        // Einfache Regel für Sofort-Meldungen – die eigentliche KI-Anomalieerkennung kommt separat
        private async Task AddTamperAlertIfNeededAsync(Slot slot, DateTime now)
        {
            var cooldownStart = now.AddSeconds(-_options.TamperAlertCooldownSeconds);
            var recentAlertExists = await db.Alerts.AnyAsync(a =>
                a.SlotId == slot.Id &&
                a.Type == AlertType.PossibleTampering &&
                !a.Resolved &&
                a.Timestamp >= cooldownStart);

            if (recentAlertExists)
            {
                return;
            }

            db.Alerts.Add(new Alert
            {
                SlotId = slot.Id,
                Type = AlertType.PossibleTampering,
                Severity = AlertSeverity.Warning,
                Message = $"Mögliche Manipulation an {slot.Name} erkannt.",
                Timestamp = now
            });
            logger.LogWarning("Mögliche Manipulation an Slot {SlotId}", slot.Id);
        }
    }
}
