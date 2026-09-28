using bikestation.Dtos;
using bikestation.Security;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/sensor-data")]
    public class SensorDataController(SensorDataService sensorDataService) : ControllerBase
    {
        // Wird von den ESP32 aufgerufen (Header X-Api-Key nötig).
        // Ungültige Payloads beantwortet [ApiController] automatisch mit 400.
        [HttpPost]
        [RequireApiKey]
        [ProducesResponseType<SensorDataResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SensorDataResponse>> Post(SensorDataRequest request)
        {
            var result = await sensorDataService.ProcessAsync(request);
            if (result is null)
            {
                return NotFound(new ProblemDetails { Title = $"Slot {request.SlotId} existiert nicht." });
            }

            return Ok(result);
        }
    }
}
