using bikestation.Dtos;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/slots")]
    public class SlotsController(SlotService slotService) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<SlotDto>>> GetAll()
        {
            return await slotService.GetAllAsync();
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType<SlotDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SlotDto>> GetById(int id)
        {
            var slot = await slotService.GetByIdAsync(id);
            return slot is null ? NotFound() : slot;
        }

        // Verlauf der letzten Messwerte eines Slots (neueste zuerst)
        [HttpGet("{id:int}/readings")]
        [ProducesResponseType<List<SensorReadingDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<List<SensorReadingDto>>> GetReadings(int id, [FromQuery] int limit = 50)
        {
            if (!await slotService.ExistsAsync(id))
            {
                return NotFound();
            }

            return await slotService.GetReadingsAsync(id, Math.Clamp(limit, 1, 500));
        }
    }
}
