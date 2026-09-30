using bikestation.Dtos;
using bikestation.Security;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/anomalies")]
    public class AnomaliesController(AnomalyService anomalyService) : ControllerBase
    {
        // Für externe Auswertungsdienste: wenn ein Dienst eine Anomalie erkannt hat (Header X-Api-Key nötig)
        [HttpPost]
        [RequireApiKey]
        [ProducesResponseType<AnomalyResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AnomalyResponse>> Post(AnomalyRequest request)
        {
            var result = await anomalyService.ReportAsync(request);
            if (result is null)
            {
                return NotFound(new ProblemDetails { Title = $"Slot {request.SlotId} existiert nicht." });
            }

            return result;
        }
    }
}
