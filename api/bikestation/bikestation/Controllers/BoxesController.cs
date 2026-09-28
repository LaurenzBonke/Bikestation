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
        public async Task<IActionResult> Release(int id) => ToResponse(await boxService.ReleaseAsync(id));

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
