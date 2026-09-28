namespace bikestation.Models
{
    public enum UserRole
    {
        User,
        Admin
    }

    // Nutzerkonto. Bewusst ohne E-Mail oder Klarnamen – nur was für Login und Box-Zuordnung nötig ist.
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;

        // Nur der Hash wird gespeichert (PBKDF2 über ASP.NET Core PasswordHasher), nie das Passwort
        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.User;
        public DateTime CreatedAt { get; set; }

        public List<Parking> Parkings { get; set; } = [];
    }
}
