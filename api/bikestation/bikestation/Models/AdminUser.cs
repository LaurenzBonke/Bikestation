namespace bikestation.Models
{
    public class AdminUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;

        // Nur der Hash wird gespeichert (PBKDF2 über ASP.NET Core PasswordHasher), nie das Passwort
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
