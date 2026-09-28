using bikestation.Dtos;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/alerts")]
    public class AlertsController(AlertService alertService) : ControllerBase
    {
        // Standard: nur offene Alerts. ?includeResolved=true zeigt auch erledigte.
        [HttpGet]
        public async Task<ActionResult<List<AlertDto>>> Get(
            [FromQuery] int? slotId,
            [FromQuery] bool includeResolved = false,
            [FromQuery] int limit = 50)
        {
            return await alertService.GetAsync(slotId, includeResolved, Math.Clamp(limit, 1, 500));
        }

        [HttpPost("{id:int}/resolve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Resolve(int id)
        {
            return await alertService.ResolveAsync(id) ? NoContent() : NotFound();
        }
    }
}
