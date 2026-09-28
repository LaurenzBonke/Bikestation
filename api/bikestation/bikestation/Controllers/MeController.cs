using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using bikestation.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Controllers
{
    // Alles zum angemeldeten Nutzer: seine Box, seine Meldungen, sein Verlauf
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public class MeController(
        BikestationDbContext db,
        IOptions<BoxOptions> boxOptions,
        IOptions<DeviceOptions> deviceOptions) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<MeDto>> Get()
        {
            var user = User.UserId() is int id ? await db.Users.FindAsync(id) : null;
            if (user is null)
            {
                return Unauthorized();
            }

            var parking = await db.Parkings.AsNoTracking()
                .Where(p => p.UserId == user.Id && p.EndedAt == null)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            MyParkingDto? parkingDto = null;
            if (parking is not null)
            {
                var slot = await db.Slots.AsNoTracking().FirstAsync(s => s.Id == parking.SlotId);
                var online = slot.LastUpdated >= DateTime.UtcNow.AddSeconds(-deviceOptions.Value.OfflineAfterSeconds);
                parkingDto = new MyParkingDto(parking.Id, slot.Id, slot.BoxState, slot.LockOpen, online,
                    parking.BookedAt, parking.ParkedAt, parking.PickupRequestedAt, Deadline(slot));
            }

            // Offene, noch nicht bestätigte Meldungen, die diesen Nutzer betreffen
            var alerts = await db.Alerts.AsNoTracking()
                .Where(a => a.UserId == user.Id && !a.AcknowledgedByUser)
                .OrderByDescending(a => a.Timestamp)
                .Take(20)
                .Select(a => new AlertDto(a.Id, a.SlotId, a.Type, a.Severity, a.Message, a.Timestamp, a.Resolved, a.Score))
                .ToListAsync();

            var history = await db.Parkings.AsNoTracking()
                .Where(p => p.UserId == user.Id && p.EndedAt != null)
                .OrderByDescending(p => p.EndedAt)
                .Take(5)
                .Select(p => new ParkingHistoryDto(p.SlotId, p.BookedAt, p.ParkedAt, p.EndedAt, p.EndReason))
                .ToListAsync();

            return new MeDto(user.Id, user.Username, user.Role, parkingDto, alerts, history);
        }

        // Nutzer hat eine Meldung gesehen – sie verschwindet aus seiner App (für den Admin bleibt sie offen)
        [HttpPost("alerts/{id:int}/ack")]
        public async Task<IActionResult> Acknowledge(int id)
        {
            var alert = await db.Alerts.FindAsync(id);
            if (alert is null || alert.UserId != User.UserId())
            {
                return NotFound();
            }

            alert.AcknowledgedByUser = true;
            await db.SaveChangesAsync();
            return NoContent();
        }

        private DateTime? Deadline(Slot slot) => slot.BoxState switch
        {
            BoxState.OpenForParking => slot.BoxStateChangedAt?.AddSeconds(boxOptions.Value.OpenForParkingTimeoutSeconds),
            BoxState.OpenForPickup => slot.BoxStateChangedAt?.AddSeconds(boxOptions.Value.OpenForPickupTimeoutSeconds),
            _ => null
        };
    }
}
