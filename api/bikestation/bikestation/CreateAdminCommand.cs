using System.Text;
using bikestation.Services;

namespace bikestation
{
    // Legt über die Kommandozeile einen Admin an, damit kein Passwort im Code oder Repo steht:
    //   dotnet run -- create-admin <benutzername>
    public static class CreateAdminCommand
    {
        private const int MinPasswordLength = 12;

        public static async Task<int> RunAsync(IServiceProvider services, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Aufruf: dotnet run -- create-admin <benutzername>");
                return 1;
            }

            var username = args[1].Trim();
            Console.Write($"Passwort für '{username}' (mind. {MinPasswordLength} Zeichen): ");
            var password = ReadPassword();

            if (password.Length < MinPasswordLength)
            {
                Console.Error.WriteLine($"Passwort muss mindestens {MinPasswordLength} Zeichen lang sein.");
                return 1;
            }

            using var scope = services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
            try
            {
                await authService.CreateAdminAsync(username, password);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            Console.WriteLine($"Admin '{username}' wurde angelegt.");
            return 0;
        }

        // Liest das Passwort ohne es anzuzeigen
        private static string ReadPassword()
        {
            if (Console.IsInputRedirected)
            {
                // PowerShell 5.1 setzt beim Pipen ein BOM davor – entfernen
                return (Console.ReadLine() ?? string.Empty).TrimStart('﻿');
            }

            var password = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return password.ToString();
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0) password.Length--;
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                }
            }
        }
    }
}
