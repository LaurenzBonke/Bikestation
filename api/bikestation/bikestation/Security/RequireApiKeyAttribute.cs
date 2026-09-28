using System.Security.Cryptography;
using System.Text;
using bikestation.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace bikestation.Security
{
    // Schützt Endpunkte, die von Geräten (ESP32) oder dem KI-Dienst aufgerufen werden.
    // Für Geräte ist ein fester Schlüssel einfacher als ein JWT-Login.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
    {
        public const string HeaderName = "X-Api-Key";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<DeviceOptions>>().Value;
            var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(provided) || !KeysMatch(provided, options.ApiKey))
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequireApiKeyAttribute>>();
                logger.LogWarning("Anfrage ohne gültigen API-Key von {Ip}", context.HttpContext.Connection.RemoteIpAddress);
                context.Result = new UnauthorizedObjectResult(new ProblemDetails { Title = "Ungültiger oder fehlender API-Key." });
            }
        }

        // Vergleich in konstanter Zeit, damit man den Key nicht über Antwortzeiten erraten kann
        private static bool KeysMatch(string provided, string expected)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected));
        }
    }
}
