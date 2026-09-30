using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/demo")]
    public class DemoController(
        DemoDataService demoDataService,
        IWebHostEnvironment environment,
        IOptions<DemoOptions> demoOptions,
        IServiceProvider services) : ControllerBase
    {
        private readonly DemoOptions _demo = demoOptions.Value;

        // Sagt dem Dashboard, ob es die virtuelle Station und die Demo-Konten anzeigen soll
        [HttpGet]
        public DemoInfo Get() => new(
            _demo.VirtualStation,
            _demo.Accounts
                .Select(a => new DemoAccountDto(a.Username, a.Password,
                    Enum.TryParse<UserRole>(a.Role, ignoreCase: true, out var role) ? role : UserRole.User))
                .ToList());

        // Füllt die Datenbank mit Beispieldaten der letzten ?days= Tage. Nur für angemeldete Admins und
        // nur im Development-Modus oder wenn Demo:Enabled=true gesetzt ist (z. B. für die Präsentation).
        [HttpPost("generate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Generate([FromQuery] int days = 7)
        {
            if (!environment.IsDevelopment() && !_demo.Enabled)
            {
                return NotFound();
            }

            var count = await demoDataService.GenerateAsync(Math.Clamp(days, 1, 30));
            return Ok(new { readings = count });
        }

        // Virtuelle Station: Riegel, LED und Abstand je Box
        [HttpGet("station")]
        public ActionResult<IReadOnlyList<VirtualBoxDto>> GetStation()
        {
            var station = VirtualStationOrNull();
            return station is null ? NotFound() : Ok(station.Boxes);
        }

        // Virtuelle Station: Fahrrad in eine Box stellen oder herausnehmen. Ohne Anmeldung, damit man in der
        // Demo auch einen Diebstahl nachstellen kann (Fahrrad aus verriegelter Box nehmen -> Alarm).
        [HttpPost("station/{slotId:int}/bike")]
        public IActionResult SetBike(int slotId, VirtualBikeRequest request)
        {
            var station = VirtualStationOrNull();
            if (station is null || !station.HasBox(slotId))
            {
                return NotFound();
            }

            station.SetBike(slotId, request.Present);
            return NoContent();
        }

        private VirtualStation? VirtualStationOrNull() =>
            _demo.VirtualStation ? services.GetService<VirtualStation>() : null;
    }
}
