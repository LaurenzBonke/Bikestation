using bikestation.Dtos;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/forecast")]
    public class ForecastController(ForecastService forecastService) : ControllerBase
    {
        // Erwartete Belegung für die nächsten ?hours= Stunden (Standard 6)
        [HttpGet]
        public async Task<ActionResult<ForecastDto>> Get([FromQuery] int hours = 6)
        {
            return await forecastService.GetAsync(Math.Clamp(hours, 1, 24));
        }
    }
}
