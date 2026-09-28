using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Services
{
    public class StatisticsService(BikestationDbContext db)
    {
        // Mindestanzahl Messungen, damit eine Stunde als "Stoßzeit" zählt
        private const int MinReadingsForBusiestHour = 10;

        public async Task<StatisticsDto> GetAsync(int days)
        {
            var since = DateTime.UtcNow.AddDays(-days);

            // Zählen passiert in der Datenbank, nicht im Speicher des Raspberry Pi.
            // Belegungsquote = Anteil der Messungen, bei denen der Platz belegt war.
            var groups = await db.SensorReadings
                .Where(r => r.Timestamp >= since)
                .GroupBy(r => new { r.SlotId, r.Timestamp.Hour })
                .Select(g => new
                {
                    g.Key.SlotId,
                    g.Key.Hour,
                    Total = g.Count(),
                    Occupied = g.Count(r => r.Occupied)
                })
                .ToListAsync();

            var alertCounts = await db.Alerts
                .Where(a => a.Timestamp >= since)
                .GroupBy(a => new { a.SlotId, a.Type })
                .Select(g => new { g.Key.SlotId, g.Key.Type, Count = g.Count() })
                .ToListAsync();

            var readingsByDay = await db.SensorReadings
                .Where(r => r.Timestamp >= since)
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new { Date = g.Key, Total = g.Count(), Occupied = g.Count(r => r.Occupied) })
                .ToListAsync();

            var alertsByDay = await db.Alerts
                .Where(a => a.Timestamp >= since)
                .GroupBy(a => new { a.Timestamp.Date, a.Type })
                .Select(g => new { g.Key.Date, g.Key.Type, Count = g.Count() })
                .ToListAsync();

            var openAlerts = await db.Alerts.CountAsync(a => !a.Resolved);
            var slots = await db.Slots.OrderBy(s => s.Id).ToListAsync();

            // Stunden liegen in UTC vor – für die Anzeige in Ortszeit umrechnen
            var utcOffset = (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalHours);
            var byHour = Enumerable.Range(0, 24).Select(localHour =>
            {
                var utcHour = ((localHour - utcOffset) % 24 + 24) % 24;
                var total = groups.Where(g => g.Hour == utcHour).Sum(g => g.Total);
                var occupied = groups.Where(g => g.Hour == utcHour).Sum(g => g.Occupied);
                return new HourlyOccupancyDto(localHour, Percent(occupied, total), total);
            }).ToList();

            var busiest = byHour
                .Where(h => h.Readings >= MinReadingsForBusiestHour)
                .OrderByDescending(h => h.OccupancyPercent)
                .FirstOrDefault();

            int CountAlerts(int? slotId, AlertType type) => alertCounts
                .Where(a => a.Type == type && (slotId == null || a.SlotId == slotId))
                .Sum(a => a.Count);

            var slotStats = slots.Select(s => new SlotStatisticsDto(
                s.Id,
                s.Name,
                Percent(groups.Where(g => g.SlotId == s.Id).Sum(g => g.Occupied), groups.Where(g => g.SlotId == s.Id).Sum(g => g.Total)),
                CountAlerts(s.Id, AlertType.PossibleTampering),
                CountAlerts(s.Id, AlertType.Anomaly))).ToList();

            var byDay = readingsByDay
                .OrderBy(d => d.Date)
                .Select(d => new DailyOccupancyDto(
                    DateOnly.FromDateTime(d.Date),
                    Percent(d.Occupied, d.Total),
                    d.Total,
                    alertsByDay.Where(a => a.Date == d.Date && a.Type == AlertType.PossibleTampering).Sum(a => a.Count),
                    alertsByDay.Where(a => a.Date == d.Date && a.Type == AlertType.Anomaly).Sum(a => a.Count)))
                .ToList();

            return new StatisticsDto(
                days,
                groups.Sum(g => g.Total),
                Percent(groups.Sum(g => g.Occupied), groups.Sum(g => g.Total)),
                busiest?.Hour,
                CountAlerts(null, AlertType.PossibleTampering),
                CountAlerts(null, AlertType.Anomaly),
                openAlerts,
                byHour,
                byDay,
                slotStats);
        }

        private static double Percent(int part, int total) => total == 0 ? 0 : Math.Round(100.0 * part / total, 1);
    }
}
