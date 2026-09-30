using bikestation.Dtos;
using bikestation.Security;
using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/alerts")]
    public class AlertsController(AlertService alertService) : ControllerBase
    {
        // Standard: nur offene Alerts. ?includeResolved=true zeigt auch erledigte.
        // Nur für Admins und Geräte – normale Nutzer sehen nur ihre eigenen Meldungen (/api/me).
        [HttpGet]
        [RequireApiKey(OrAdmin = true)]
        public async Task<ActionResult<List<AlertDto>>> Get(
            [FromQuery] int? slotId,
            [FromQuery] bool includeResolved = false,
            [FromQuery] int limit = 50)
        {
            return await alertService.GetAsync(slotId, includeResolved, Math.Clamp(limit, 1, 500));
        }

        // Nur für angemeldete Admins (JWT im Authorization-Header)
        [HttpPost("{id:int}/resolve")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Resolve(int id)
        {
            return await alertService.ResolveAsync(id) ? NoContent() : NotFound();
        }
    }
}
