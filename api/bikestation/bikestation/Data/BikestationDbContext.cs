using bikestation.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace bikestation.Data
{
    public class BikestationDbContext(DbContextOptions<BikestationDbContext> options) : DbContext(options)
    {
        public DbSet<Slot> Slots => Set<Slot>();
        public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
        public DbSet<Alert> Alerts => Set<Alert>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Parking> Parkings => Set<Parking>();

        // SQLite speichert keine Zeitzone – beim Lesen alle DateTime-Werte als UTC markieren,
        // damit die API "...Z" ausgibt und das Frontend korrekt umrechnet
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
            v => v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Enums als Text speichern, damit die DB auch ohne Code lesbar ist
            modelBuilder.Entity<Slot>().Property(s => s.Status).HasConversion<string>();
            modelBuilder.Entity<Slot>().Property(s => s.BoxState).HasConversion<string>();
            modelBuilder.Entity<Alert>().Property(a => a.Type).HasConversion<string>();
            modelBuilder.Entity<Alert>().Property(a => a.Severity).HasConversion<string>();
            modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>();
            modelBuilder.Entity<Parking>().Property(p => p.EndReason).HasConversion<string>();

            modelBuilder.Entity<SensorReading>().HasIndex(r => new { r.SlotId, r.Timestamp });
            modelBuilder.Entity<Alert>().HasIndex(a => new { a.SlotId, a.Timestamp });
            modelBuilder.Entity<Alert>().HasIndex(a => a.UserId);
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Parking>().HasIndex(p => new { p.UserId, p.EndedAt });

            modelBuilder.Entity<Slot>().Ignore(s => s.LockOpen);

            modelBuilder.Entity<Parking>()
                .HasOne(p => p.Slot)
                .WithMany()
                .HasForeignKey(p => p.SlotId);
            modelBuilder.Entity<Parking>()
                .HasOne(p => p.User)
                .WithMany(u => u.Parkings)
                .HasForeignKey(p => p.UserId);

            // Die 3 Boxen des Prototyps
            modelBuilder.Entity<Slot>().HasData(
                new Slot { Id = 1, Name = "Stellplatz 1" },
                new Slot { Id = 2, Name = "Stellplatz 2" },
                new Slot { Id = 3, Name = "Stellplatz 3" });
        }
    }
}
