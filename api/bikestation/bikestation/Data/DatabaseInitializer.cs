using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using bikestation.Models;
using bikestation.Options;

namespace bikestation.Data
{
    // Legt die SQLite-Datenbank an und bringt eine ältere Datenbank automatisch auf das aktuelle Datenmodell.
    //
    // EnsureCreated kann bestehende Tabellen nicht ändern. Passt die Datei nicht mehr zum Modell, wird sie
    // gesichert, das neue Schema in einer frischen Datei angelegt und alle vorhandenen Daten spaltenweise
    // übernommen. So überleben Admin-Konten und Messwerte ein Update – auch auf dem Server im Produktionsbetrieb.
    // (Spätestens mit der Datenbank auf der Proxmox-VM sollten wir auf EF-Core-Migrations umstellen.)
    public static class DatabaseInitializer
    {
        // Tabellen, die umbenannt wurden: neue Tabelle <- alte Tabelle, mit festen Werten für neue Spalten
        private static readonly (string Table, string OldTable, Dictionary<string, string> Values)[] RenamedTables =
        [
            ("Users", "AdminUsers", new() { ["Role"] = "'Admin'" }),
        ];

        public static void Initialize(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<BikestationDbContext>>();

            var file = Path.GetFullPath(new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource);
            var directory = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            db.Database.EnsureCreated();
            logger.LogInformation("Datenbank: {File}", file);
            if (!SchemaIsCurrent(db))
            {
                db.Database.CloseConnection();
                var backup = Migrate(db, file);
                logger.LogWarning(
                    "Datenbank wurde auf das neue Datenmodell umgestellt, vorhandene Daten übernommen. Sicherung: {Backup}", backup);
            }

            var stationCount = Math.Max(1, scope.ServiceProvider.GetRequiredService<IOptions<BoxOptions>>().Value.StationCount);
            SyncStations(db, stationCount, logger);
            BackfillBoxEvents(db, logger);
        }

        // Ältere Datenbanken haben noch kein Box-Protokoll: aus den gespeicherten Parkvorgängen nachtragen
        public static void BackfillBoxEvents(BikestationDbContext db, ILogger logger)
        {
            if (db.BoxEvents.Any() || !db.Parkings.Any()) return;

            foreach (var p in db.Parkings.AsNoTracking().ToList())
            {
                void Add(BoxEventType type, DateTime? at)
                {
                    if (at is DateTime time) db.BoxEvents.Add(new BoxEvent { SlotId = p.SlotId, UserId = p.UserId, Type = type, Timestamp = time });
                }
                Add(BoxEventType.Booked, p.BookedAt);
                Add(BoxEventType.Parked, p.ParkedAt);
                Add(BoxEventType.PickupRequested, p.PickupRequestedAt);
                Add(p.EndReason switch
                {
                    ParkingEndReason.Cancelled => BoxEventType.Cancelled,
                    ParkingEndReason.TimedOut => BoxEventType.ParkingTimedOut,
                    ParkingEndReason.BikeRemoved => BoxEventType.BikeRemoved,
                    _ => BoxEventType.PickedUp
                }, p.EndedAt);
            }
            var count = db.SaveChanges();
            logger.LogInformation("Box-Protokoll aus {Count} gespeicherten Ereignissen nachgetragen", count);
        }

        // Legt die Boxen 1..N an und entfernt Boxen darüber (samt Messwerten, Meldungen und Parkvorgängen)
        public static void SyncStations(BikestationDbContext db, int count, ILogger logger)
        {
            var existing = db.Slots.Select(s => s.Id).ToHashSet();
            for (var id = 1; id <= count; id++)
            {
                if (!existing.Contains(id)) db.Slots.Add(new Slot { Id = id, Name = $"Stellplatz {id}" });
            }
            db.SaveChanges();

            if (existing.Any(id => id > count))
            {
                db.Alerts.Where(a => a.SlotId > count).ExecuteDelete();
                db.SensorReadings.Where(r => r.SlotId > count).ExecuteDelete();
                db.Parkings.Where(p => p.SlotId > count).ExecuteDelete();
                var removed = db.Slots.Where(s => s.Id > count).ExecuteDelete();
                logger.LogWarning("{Removed} Box(en) über Nummer {Count} entfernt (Box:StationCount = {Count})", removed, count, count);
            }
        }

        // Gibt den Pfad der Sicherungskopie zurück
        public static string Migrate(BikestationDbContext db, string file)
        {
            SqliteConnection.ClearAllPools();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backup = $"{file}.vor-update-{stamp}";
            var newFile = $"{file}.neu-{stamp}";

            // 1. Konsistente Sicherung über die SQLite-Backup-Funktion (inkl. noch nicht geschriebener WAL-Daten)
            using (var source = new SqliteConnection($"Data Source={file}"))
            using (var target = new SqliteConnection($"Data Source={backup}"))
            {
                source.Open();
                target.Open();
                source.BackupDatabase(target);
            }

            // 2. Neues Schema in einer frischen Datei anlegen
            var options = new DbContextOptionsBuilder<BikestationDbContext>().UseSqlite($"Data Source={newFile}").Options;
            using (var fresh = new BikestationDbContext(options))
            {
                fresh.Database.EnsureCreated();
            }

            // 3. Daten aus der Sicherung übernehmen
            using (var connection = new SqliteConnection($"Data Source={newFile}"))
            {
                connection.Open();
                // Ohne Fremdschlüssel-Prüfung: INSERT OR REPLACE auf die Start-Boxen würde sonst per Kaskade
                // bereits kopierte Messwerte und Meldungen wieder löschen
                Execute(connection, "PRAGMA foreign_keys = OFF");
                Execute(connection, $"ATTACH DATABASE '{backup.Replace("'", "''")}' AS old");
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var entity in db.Model.GetEntityTypes())
                    {
                        CopyTable(connection, transaction, entity);
                    }
                    transaction.Commit();
                }
                Execute(connection, "DETACH DATABASE old");
            }

            // 4. Neue Datei an die Stelle der alten setzen
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(file + suffix)) File.Delete(file + suffix);
            }
            File.Move(newFile, file);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                if (File.Exists(newFile + suffix)) File.Delete(newFile + suffix);
            }
            return backup;
        }

        private static void CopyTable(SqliteConnection connection, SqliteTransaction transaction, IEntityType entity)
        {
            var table = entity.GetTableName();
            if (table is null) return;

            var oldTable = table;
            var fixedValues = new Dictionary<string, string>();
            var oldColumns = Columns(connection, "old", oldTable);
            if (oldColumns.Count == 0)
            {
                var renamed = RenamedTables.FirstOrDefault(r => r.Table == table);
                if (renamed.Table is null) return;
                oldTable = renamed.OldTable;
                fixedValues = renamed.Values;
                oldColumns = Columns(connection, "old", oldTable);
                if (oldColumns.Count == 0) return;
            }

            var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var targetColumns = new List<string>();
            var selectExpressions = new List<string>();
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(storeObject);
                if (column is null) continue;

                string? expression = null;
                if (fixedValues.TryGetValue(column, out var fixedValue)) expression = fixedValue;
                else if (oldColumns.Contains(column)) expression = $"\"{column}\"";
                else if (!property.IsNullable) expression = DefaultFor(property);

                if (expression is null) continue;  // neue, optionale Spalte bleibt NULL
                targetColumns.Add($"\"{column}\"");
                selectExpressions.Add(expression);
            }

            // INSERT OR REPLACE, falls das neue Schema schon Startdaten enthält
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"INSERT OR REPLACE INTO main.\"{table}\" ({string.Join(", ", targetColumns)}) " +
                $"SELECT {string.Join(", ", selectExpressions)} FROM old.\"{oldTable}\"";
            command.ExecuteNonQuery();
        }

        // Startwert für eine neue Pflichtspalte: erster Enum-Wert, 0, leerer Text oder Mindestdatum
        private static string DefaultFor(IProperty property)
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type.IsEnum) return $"'{Enum.GetNames(type)[0]}'";
            if (type == typeof(string)) return "''";
            if (type == typeof(bool)) return "0";
            if (type == typeof(DateTime)) return "'0001-01-01 00:00:00'";
            return "0";
        }

        private static HashSet<string> Columns(SqliteConnection connection, string schema, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA {schema}.table_info(\"{table}\")";
            using var reader = command.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
            return columns;
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        // Vergleicht Tabellen und Spalten des Datenmodells mit denen in der SQLite-Datei
        private static bool SchemaIsCurrent(BikestationDbContext db)
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            db.Database.OpenConnection();
            try
            {
                foreach (var entity in db.Model.GetEntityTypes())
                {
                    var table = entity.GetTableName();
                    if (table is null) continue;

                    var existing = Columns(connection, "main", table);
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
