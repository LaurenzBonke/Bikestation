using bikestation.Data;
using bikestation.Dtos;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Services
{
    // Auslastungsprognose aus historischen Daten: Wie voll war die Station in den letzten Wochen
    // am selben Wochentag zur selben Stunde? Gibt es dafür zu wenig Daten, wird die gleiche Stunde
    // über alle Tage verwendet.
    public class ForecastService(BikestationDbContext db)
    {
        private const int HistoryDays = 28;
        private const int MinReadings = 10;

        public async Task<ForecastDto> GetAsync(int hours)
        {
            var since = DateTime.UtcNow.AddDays(-HistoryDays);

            // Zählen in der Datenbank, gruppiert nach UTC-Wochentag und -Stunde
            var groups = await db.SensorReadings
                .Where(r => r.Timestamp >= since)
                .GroupBy(r => new { r.Timestamp.DayOfWeek, r.Timestamp.Hour })
                .Select(g => new { g.Key.DayOfWeek, g.Key.Hour, Total = g.Count(), Occupied = g.Count(r => r.Occupied) })
                .ToListAsync();
            var totalSlots = await db.Slots.CountAsync();

            var now = DateTime.UtcNow;
            var firstHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);

            var result = Enumerable.Range(0, hours).Select(i =>
            {
                var start = firstHour.AddHours(i);
                var sameSlot = groups.Where(g => g.DayOfWeek == start.DayOfWeek && g.Hour == start.Hour).ToList();
                if (sameSlot.Sum(g => g.Total) < MinReadings)
                {
                    sameSlot = groups.Where(g => g.Hour == start.Hour).ToList();
                }

                var total = sameSlot.Sum(g => g.Total);
                var local = start.ToLocalTime();
                if (total < MinReadings)
                {
                    return new ForecastHourDto(start, local.Hour, null, null, total);
                }

                var rate = (double)sameSlot.Sum(g => g.Occupied) / total;
                return new ForecastHourDto(
                    start,
                    local.Hour,
                    Math.Round(rate * 100, 1),
                    Math.Round(totalSlots * (1 - rate), 1),
                    total);
            }).ToList();

            return new ForecastDto(totalSlots, HistoryDays, result);
        }
    }
}
