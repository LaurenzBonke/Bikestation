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
        BoxService boxService,
        IOptions<OccupancyOptions> options,
        ILogger<SensorDataService> logger)
    {
        private readonly OccupancyOptions _options = options.Value;

        // Gibt null zurück, wenn der Slot nicht existiert
        public Task<SensorDataResponse?> ProcessAsync(SensorDataRequest request) => BoxService.LockedAsync(async () =>
        {
            var slot = await db.Slots.FindAsync(request.SlotId!.Value);
            if (slot is null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var distance = request.Distance!.Value;

            // Belegt, wenn der Ultraschallsensor ein Fahrrad direkt vor sich sieht oder der Drucksensor Last meldet
            var occupied = boxService.IsBikePresent(distance) || request.Pressure!.Value >= _options.PressureThreshold;

            db.SensorReadings.Add(new SensorReading
            {
                SlotId = slot.Id,
                Pressure = request.Pressure!.Value,
                Distance = distance,
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

            // Box-Zustand (Riegel, Alarm) anhand der Messung weiterschalten
            await boxService.OnReadingAsync(slot, distance, now);

            if (request.Vibration.Value)
            {
                await AddTamperAlertIfNeededAsync(slot, now);
            }

            await db.SaveChangesAsync();

            return new SensorDataResponse(slot.Id, occupied, slot.Status, slot.BoxState, slot.LockOpen);
        });

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
                // Steht ein Fahrrad in der Box, bekommt auch der Besitzer die Meldung in der App
                UserId = await boxService.OwnerOfAsync(slot),
                Type = AlertType.PossibleTampering,
                Severity = AlertSeverity.Warning,
                Message = $"Mögliche Manipulation an {slot.Name} erkannt.",
                Timestamp = now
            });
            logger.LogWarning("Mögliche Manipulation an Slot {SlotId}", slot.Id);
        }
    }
}
