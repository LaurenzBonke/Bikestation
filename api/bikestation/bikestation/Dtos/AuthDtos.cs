using System.ComponentModel.DataAnnotations;
using bikestation.Models;

namespace bikestation.Dtos
{
    public class LoginRequest
    {
        [Required]
        [StringLength(64)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string Password { get; set; } = string.Empty;
    }

    // Registrierung: bewusst nur Benutzername und Passwort – keine E-Mail, kein Klarname (Datensparsamkeit)
    public class RegisterRequest
    {
        [Required]
        [RegularExpression("^[A-Za-z0-9_.-]{3,32}$",
            ErrorMessage = "Benutzername: 3–32 Zeichen, nur Buchstaben, Ziffern, _ . -")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Passwort: mindestens 8 Zeichen.")]
        public string Password { get; set; } = string.Empty;
    }

    public record LoginResponse(string Token, DateTime ExpiresAt, string Username, UserRole Role);

    public record CurrentUserResponse(int Id, string Username, UserRole Role);
}
