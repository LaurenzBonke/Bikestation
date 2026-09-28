using System.ComponentModel.DataAnnotations;

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

    public record LoginResponse(string Token, DateTime ExpiresAt, string Username);

    public record CurrentUserResponse(string Username);
}
