using bikestation.Data;
using bikestation.Dtos;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Services
{
    public class AlertService(BikestationDbContext db)
    {
        public async Task<List<AlertDto>> GetAsync(int? slotId, bool includeResolved, int limit)
        {
            var query = db.Alerts.AsQueryable();

            if (slotId is not null)
            {
                query = query.Where(a => a.SlotId == slotId);
            }
            if (!includeResolved)
            {
                query = query.Where(a => !a.Resolved);
            }

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .Select(a => new AlertDto(a.Id, a.SlotId, a.Type, a.Severity, a.Message, a.Timestamp, a.Resolved, a.Score))
                .ToListAsync();
        }

        // Gibt false zurück, wenn der Alert nicht existiert
        public async Task<bool> ResolveAsync(int id)
        {
            var alert = await db.Alerts.FindAsync(id);
            if (alert is null)
            {
                return false;
            }

            alert.Resolved = true;
            await db.SaveChangesAsync();
            return true;
        }
    }
}
