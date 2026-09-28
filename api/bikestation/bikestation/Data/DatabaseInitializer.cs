using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace bikestation.Data
{
    // Legt die SQLite-Datenbank an und erkennt, wenn sie zu einem älteren Datenmodell gehört.
    // EnsureCreated kann bestehende Tabellen nicht ändern – ohne diese Prüfung liefern einzelne
    // Endpunkte dann nur Fehler 500 ("no such column"). Bis wir auf EF-Core-Migrations umstellen
    // (spätestens mit der Datenbank auf der Proxmox-VM), wird eine veraltete Datei im
    // Development-Modus gesichert und neu angelegt.
    public static class DatabaseInitializer
    {
        public static void Initialize(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<BikestationDbContext>>();

            db.Database.EnsureCreated();
            if (SchemaIsCurrent(db))
            {
                return;
            }

            var file = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
            if (!app.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Die Datenbank '{file}' passt nicht zum aktuellen Datenmodell. Bitte sichern und neu anlegen.");
            }

            var backup = $"{file}.veraltet-{DateTime.Now:yyyyMMdd-HHmmss}";
            db.Database.CloseConnection();
            SqliteConnection.ClearAllPools();
            File.Move(file, backup);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                if (File.Exists(file + suffix)) File.Delete(file + suffix);
            }

            db.Database.EnsureCreated();
            logger.LogWarning(
                "Datenbank war veraltet und wurde neu angelegt. Alte Datei gesichert als {Backup}. " +
                "Admin neu anlegen: dotnet run -- create-admin <name>", backup);
        }

        // Vergleicht Tabellen und Spalten des Datenmodells mit denen in der SQLite-Datei
        private static bool SchemaIsCurrent(BikestationDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            db.Database.OpenConnection();
            try
            {
                foreach (var entity in db.Model.GetEntityTypes())
                {
                    var table = entity.GetTableName();
                    if (table is null) continue;

                    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using var command = connection.CreateCommand();
                    command.CommandText = $"PRAGMA table_info(\"{table}\")";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) existing.Add(reader.GetString(1));
                    }

                    var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
                    var expected = entity.GetProperties().Select(p => p.GetColumnName(storeObject)).OfType<string>();
                    if (existing.Count == 0 || expected.Any(column => !existing.Contains(column)))
                    {
                        return false;
                    }
                }
                return true;
            }
            finally
            {
                db.Database.CloseConnection();
            }
        }
    }
}
