using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using bikestation.Security;
using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/boxes")]
    public class BoxesController(
        BikestationDbContext db,
        BoxService boxService,
        IOptions<DeviceOptions> deviceOptions) : ControllerBase
    {
        // Alle Boxen mit Zustand. Angemeldete Nutzer sehen zusätzlich, welche ihre ist.
        [HttpGet]
        public async Task<ActionResult<List<BoxDto>>> GetAll()
        {
            var userId = User.UserId();
            var ownedSlotId = userId is int id
                ? await db.Parkings.Where(p => p.UserId == id && p.EndedAt == null).Select(p => (int?)p.SlotId).FirstOrDefaultAsync()
                : null;
            var onlineSince = DateTime.UtcNow.AddSeconds(-deviceOptions.Value.OfflineAfterSeconds);

            var slots = await db.Slots.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
            return slots.Select(s => new BoxDto(
                s.Id,
                s.Name,
                s.Location,
                s.BoxState,
                s.LockOpen,
                s.Status == SlotStatus.Occupied,
                s.LastUpdated != null && s.LastUpdated >= onlineSince,
                s.Id == ownedSlotId,
                s.BoxStateChangedAt)).ToList();
        }

        // Freie Box buchen: Riegel öffnet, Nutzer stellt sein Fahrrad hinein
        [HttpPost("{id:int}/book")]
        [Authorize]
        public Task<IActionResult> Book(int id) => Run(userId => boxService.BookAsync(userId, id));

        // Buchung abbrechen, solange noch kein Fahrrad drin ist
        [HttpPost("{id:int}/cancel")]
        [Authorize]
        public Task<IActionResult> Cancel(int id) => Run(userId => boxService.CancelAsync(userId, id));

        // Eigene, verriegelte Box zum Abholen öffnen
        [HttpPost("{id:int}/pickup")]
        [Authorize]
        public Task<IActionResult> Pickup(int id) => Run(userId => boxService.RequestPickupAsync(userId, id));

        // Nach einem Alarm gesperrte Box nach Kontrolle wieder freigeben
        [HttpPost("{id:int}/release")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Release(int id) => ToResponse(await boxService.ReleaseAsync(id, User.UserId()));

        // Belegung: welcher Nutzer ist gerade an welcher Box (nur Admins)
        [HttpGet("occupancy")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<BoxOccupancyDto>>> Occupancy()
        {
            var onlineSince = DateTime.UtcNow.AddSeconds(-deviceOptions.Value.OfflineAfterSeconds);
            var slots = await db.Slots.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
            var parkingIds = slots.Where(s => s.ActiveParkingId != null).Select(s => s.ActiveParkingId!.Value).ToList();
            var parkings = await db.Parkings.AsNoTracking()
                .Where(p => parkingIds.Contains(p.Id))
                .Select(p => new { p.Id, p.User!.Username, p.BookedAt, p.ParkedAt })
                .ToDictionaryAsync(p => p.Id);

            return slots.Select(s =>
            {
                var parking = s.ActiveParkingId is int id && parkings.TryGetValue(id, out var p) ? p : null;
                return new BoxOccupancyDto(
                    s.Id,
                    s.Name,
                    s.Location,
                    s.BoxState,
                    s.LockOpen,
                    s.LastUpdated != null && s.LastUpdated >= onlineSince,
                    parking?.Username,
                    parking?.BookedAt,
                    parking?.ParkedAt,
                    s.BoxStateChangedAt);
            }).ToList();
        }

        // Protokoll: wer hat wann welche Box gebucht, geöffnet, geschlossen – neueste zuerst
        [HttpGet("events")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<BoxEventDto>>> Events([FromQuery] int limit = 200, [FromQuery] int? slotId = null)
        {
            limit = Math.Clamp(limit, 1, 1000);
            var query = db.BoxEvents.AsNoTracking();
            if (slotId is int sid) query = query.Where(e => e.SlotId == sid);
            var events = await query
                .OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id)
                .Take(limit)
                .Select(e => new { e.Id, e.SlotId, SlotName = e.Slot!.Name, e.Slot.Location, Username = e.User == null ? null : e.User.Username, e.Type, e.Timestamp })
                .ToListAsync();
            return events.Select(e => new BoxEventDto(
                e.Id, e.SlotId, e.SlotName, e.Location, e.Username, e.Type, BoxEvent.OpensLock(e.Type), e.Timestamp)).ToList();
        }

        private async Task<IActionResult> Run(Func<int, Task<BoxActionResult>> action)
        {
            if (User.UserId() is not int userId)
            {
                return Unauthorized();
            }
            return ToResponse(await action(userId));
        }

        private IActionResult ToResponse(BoxActionResult result) => result switch
        {
            BoxActionResult.Ok => NoContent(),
            BoxActionResult.NotFound => NotFound(),
            BoxActionResult.NotAllowed => Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Das ist nicht deine Box.", type: "NotAllowed"),
            BoxActionResult.AlreadyHasBox => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Du hast bereits eine Box.", type: "AlreadyHasBox"),
            BoxActionResult.Offline => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Die Station ist gerade nicht verbunden.", type: "Offline"),
            _ => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Die Box ist gerade nicht in dem passenden Zustand.", type: "WrongState"),
        };
    }
}
