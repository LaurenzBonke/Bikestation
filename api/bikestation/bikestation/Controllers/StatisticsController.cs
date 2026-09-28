using bikestation.Dtos;
using bikestation.Services;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/statistics")]
    public class StatisticsController(StatisticsService statisticsService) : ControllerBase
    {
        // Auslastung und Ereignisse der letzten ?days= Tage (Standard 7)
        [HttpGet]
        public async Task<ActionResult<StatisticsDto>> Get([FromQuery] int days = 7)
        {
            return await statisticsService.GetAsync(Math.Clamp(days, 1, 90));
        }
    }
}
