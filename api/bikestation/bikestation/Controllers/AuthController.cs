using System.IdentityModel.Tokens.Jwt;
using bikestation.Dtos;
using bikestation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace bikestation.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(AuthService authService) : ControllerBase
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

        // Prüft, ob der mitgeschickte Token gültig ist
        [HttpGet("me")]
        [Authorize]
        public ActionResult<CurrentUserResponse> Me()
        {
            var username = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? string.Empty;
            return new CurrentUserResponse(username);
        }
    }
}
