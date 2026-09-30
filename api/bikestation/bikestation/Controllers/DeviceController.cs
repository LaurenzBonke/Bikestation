using bikestation.Data;
using bikestation.Dtos;
using bikestation.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Controllers
{
    // Schnittstelle für die Geräte (Raspberry Pi), geschützt per API-Key
    [ApiController]
    [Route("api/device")]
    [RequireApiKey]
    public class DeviceController(BikestationDbContext db) : ControllerBase
    {
        // Soll-Zustand der Riegel aller Boxen – das Gerät stellt die Servos entsprechend
        [HttpGet("boxes")]
        public async Task<ActionResult<List<DeviceBoxDto>>> Boxes()
        {
            var slots = await db.Slots.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
            return slots.Select(s => new DeviceBoxDto(s.Id, s.BoxState, s.LockOpen)).ToList();
        }
    }
}
