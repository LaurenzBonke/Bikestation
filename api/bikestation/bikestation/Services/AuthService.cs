using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using bikestation.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace bikestation.Services
{
    public class AuthService(
        BikestationDbContext db,
        IOptions<JwtOptions> options,
        ILogger<AuthService> logger)
    {
        private readonly JwtOptions _options = options.Value;
        private readonly PasswordHasher<User> _hasher = new();

        // Gibt null zurück, wenn Benutzername oder Passwort falsch sind
        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user is null)
            {
                logger.LogWarning("Fehlgeschlagener Login für unbekannten Benutzer");
                return null;
            }

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                logger.LogWarning("Fehlgeschlagener Login für {Username}", user.Username);
                return null;
            }

            // Hash auf aktuellen Algorithmus aktualisieren, falls nötig
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _hasher.HashPassword(user, request.Password);
                await db.SaveChangesAsync();
            }

            logger.LogInformation("Login erfolgreich: {Username}", user.Username);
            return CreateToken(user);
        }

        // Neues Nutzerkonto (Rolle User). Gibt null zurück, wenn der Name schon vergeben ist.
        public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
        {
            var username = request.Username.Trim();
            if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            {
                return null;
            }

            var user = new User { Username = username, Role = UserRole.User, CreatedAt = DateTime.UtcNow };
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            logger.LogInformation("Neues Nutzerkonto: {Username}", user.Username);
            return CreateToken(user);
        }

        public async Task CreateAdminAsync(string username, string password)
        {
            if (await db.Users.AnyAsync(u => u.Username == username))
            {
                throw new InvalidOperationException($"Benutzer '{username}' existiert bereits.");
            }

            var user = new User { Username = username, Role = UserRole.Admin, CreatedAt = DateTime.UtcNow };
            user.PasswordHash = _hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        private LoginResponse CreateToken(User user)
        {
            var minutes = user.Role == UserRole.Admin ? _options.ExpiresMinutes : _options.UserExpiresMinutes;
            var expiresAt = DateTime.UtcNow.AddMinutes(minutes);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                ],
                expires: expiresAt,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, user.Username, user.Role);
        }
    }
}
