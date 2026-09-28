using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/demo")]
    public class DemoController(DemoDataService demoDataService, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
    {
        // Füllt die Datenbank mit Beispieldaten der letzten ?days= Tage. Nur für angemeldete Admins und
        // nur im Development-Modus oder wenn Demo:Enabled=true gesetzt ist (z. B. für die Präsentation).
        [HttpPost("generate")]
        [Authorize]
        public async Task<IActionResult> Generate([FromQuery] int days = 7)
        {
            if (!environment.IsDevelopment() && !configuration.GetValue<bool>("Demo:Enabled"))
            {
                return NotFound();
            }

            var count = await demoDataService.GenerateAsync(Math.Clamp(days, 1, 30));
            return Ok(new { readings = count });
        }
    }
}
