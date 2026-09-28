using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/demo")]
    public class DemoController(DemoDataService demoDataService, IWebHostEnvironment environment) : ControllerBase
    {
        // Füllt die Datenbank mit Beispieldaten der letzten ?days= Tage.
        // Nur im Development-Modus und nur für angemeldete Admins.
        [HttpPost("generate")]
        [Authorize]
        public async Task<IActionResult> Generate([FromQuery] int days = 7)
        {
            if (!environment.IsDevelopment())
            {
                return NotFound();
            }

            var count = await demoDataService.GenerateAsync(Math.Clamp(days, 1, 30));
            return Ok(new { readings = count });
        }
    }
}
