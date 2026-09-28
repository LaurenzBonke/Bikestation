using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Services
{
    public class SlotService(BikestationDbContext db)
    {
        public async Task<List<SlotDto>> GetAllAsync()
        {
            return await db.Slots
                .OrderBy(s => s.Id)
                .Select(ToDto)
                .ToListAsync();
        }

        public async Task<SlotDto?> GetByIdAsync(int id)
        {
            return await db.Slots
                .Where(s => s.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();
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

        public Task<bool> ExistsAsync(int id) => db.Slots.AnyAsync(s => s.Id == id);

        // Wird von EF Core in SQL übersetzt
        private static readonly System.Linq.Expressions.Expression<Func<Slot, SlotDto>> ToDto = s => new SlotDto(
            s.Id,
            s.Name,
            s.Status,
            s.Status == SlotStatus.Occupied ? "Belegt" : s.Status == SlotStatus.Free ? "Frei" : "Unbekannt",
            s.LastUpdated,
            s.Alerts.Any(a => a.Type == AlertType.PossibleTampering && !a.Resolved),
            s.Readings
                .OrderByDescending(r => r.Timestamp)
                .Select(r => new SensorReadingDto(r.Pressure, r.Distance, r.Vibration, r.Occupied, r.Timestamp))
                .FirstOrDefault());
    }
}
