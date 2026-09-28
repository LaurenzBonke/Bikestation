using bikestation.Dtos;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/sensor-readings")]
    public class SensorReadingsController(SlotService slotService) : ControllerBase
    {
        // Messwerte aller Slots, älteste zuerst. Mit ?afterId= holt man nur neue Werte (für den KI-Dienst).
        [HttpGet]
        public async Task<ActionResult<List<SlotReadingDto>>> Get([FromQuery] long afterId = 0, [FromQuery] int limit = 1000)
        {
            return await slotService.GetReadingsAfterAsync(afterId, Math.Clamp(limit, 1, 5000));
        }
    }
}
