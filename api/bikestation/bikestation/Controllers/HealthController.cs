using bikestation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController(BikestationDbContext db) : ControllerBase
    {
        // Für Überwachung und Fehlersuche: läuft die API, ist die Datenbank erreichbar?
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var databaseOk = await db.Database.CanConnectAsync();
            var body = new
            {
                status = databaseOk ? "ok" : "fehler",
                database = databaseOk,
                serverTime = DateTime.UtcNow
            };
            return databaseOk ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
        }
    }
}
