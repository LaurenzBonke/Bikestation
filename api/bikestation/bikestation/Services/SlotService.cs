using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    public class SlotService(BikestationDbContext db, IOptions<DeviceOptions> deviceOptions)
    {
        public async Task<List<SlotDto>> GetAllAsync()
        {
            return await Project(db.Slots.OrderBy(s => s.Id)).ToListAsync();
        }

        public async Task<SlotDto?> GetByIdAsync(int id)
        {
            return await Project(db.Slots.Where(s => s.Id == id)).FirstOrDefaultAsync();
        }

        public async Task<List<SensorReadingDto>> GetReadingsAsync(int slotId, int limit)
        {
            return await db.SensorReadings
                .Where(r => r.SlotId == slotId)
                .OrderByDescending(r => r.Timestamp)
                .Take(limit)
                .Select(r => new SensorReadingDto(r.Pressure, r.Distance, r.Vibration, r.Occupied, r.Timestamp))
                .ToListAsync();
        }

        // Messwerte aller Slots nach einer bestimmten ID (älteste zuerst) – für den KI-Dienst
        public async Task<List<SlotReadingDto>> GetReadingsAfterAsync(long afterId, int limit)
        {
            return await db.SensorReadings
                .Where(r => r.Id > afterId)
                .OrderBy(r => r.Id)
                .Take(limit)
                .Select(r => new SlotReadingDto(r.Id, r.SlotId, r.Pressure, r.Distance, r.Vibration, r.Occupied, r.Timestamp))
                .ToListAsync();
        }

        public Task<bool> ExistsAsync(int id) => db.Slots.AnyAsync(s => s.Id == id);

        // Wird von EF Core in SQL übersetzt
        private IQueryable<SlotDto> Project(IQueryable<Slot> slots)
        {
            var onlineSince = DateTime.UtcNow.AddSeconds(-deviceOptions.Value.OfflineAfterSeconds);

            return slots.Select(s => new SlotDto(
                s.Id,
                s.Name,
                s.Status,
                s.Status == SlotStatus.Occupied ? "Belegt" : s.Status == SlotStatus.Free ? "Frei" : "Unbekannt",
                s.LastUpdated,
                s.LastUpdated != null && s.LastUpdated >= onlineSince,
                s.BoxState,
                s.Alerts.Any(a => (a.Type == AlertType.PossibleTampering || a.Type == AlertType.BikeRemoved) && !a.Resolved),
                s.Alerts.Any(a => a.Type == AlertType.Anomaly && !a.Resolved),
                s.Readings
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => new SensorReadingDto(r.Pressure, r.Distance, r.Vibration, r.Occupied, r.Timestamp))
                    .FirstOrDefault()));
        }
    }
}
