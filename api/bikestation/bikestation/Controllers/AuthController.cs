using bikestation.Data;
using bikestation.Dtos;
using bikestation.Security;
using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(AuthService authService, BikestationDbContext db) : ControllerBase
    {
        // Gibt bei Erfolg einen JWT zurück. Begrenzt auf wenige Versuche pro Minute (siehe Program.cs).
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var result = await authService.LoginAsync(request);
            if (result is null)
            {
                // Bewusst gleiche Meldung für falschen Benutzer und falsches Passwort
                return Unauthorized(new ProblemDetails { Title = "Benutzername oder Passwort ist falsch." });
            }

            return result;
        }

        // Neues Nutzerkonto anlegen, meldet direkt an
        [HttpPost("register")]
        [EnableRateLimiting("register")]
        [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request)
        {
            var result = await authService.RegisterAsync(request);
            if (result is null)
            {
                return Conflict(new ProblemDetails { Title = "Dieser Benutzername ist schon vergeben." });
            }

            return result;
        }

        // Prüft, ob der mitgeschickte Token gültig ist und das Konto noch existiert
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<CurrentUserResponse>> Me()
        {
            var user = User.UserId() is int id ? await db.Users.FindAsync(id) : null;
            if (user is null)
            {
                return Unauthorized();
            }

            return new CurrentUserResponse(user.Id, user.Username, user.Role);
        }
    }
}
