using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace bikestation.Security
{
    // Liest Nutzer-ID und -Name aus dem geprüften JWT
    public static class UserClaims
    {
        public static int? UserId(this ClaimsPrincipal user) =>
            int.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

        public static string Username(this ClaimsPrincipal user) =>
            user.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? string.Empty;
    }
}
