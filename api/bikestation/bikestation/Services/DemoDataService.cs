using bikestation.Data;
using bikestation.Models;
using bikestation.Options;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    // Erzeugt realistische Messdaten der letzten Tage – nur für Entwicklung und Präsentation,
    // damit Statistik und KI schon ohne echte Hardware etwas zeigen.
    public class DemoDataService(BikestationDbContext db, IOptions<OccupancyOptions> options)
    {
        private const int StepMinutes = 5;

        public async Task<int> GenerateAsync(int days)
        {
            var random = new Random(42);
            var threshold = options.Value.PressureThreshold;
            var end = DateTime.UtcNow;
            var start = end.AddDays(-days);
            var slots = db.Slots.Select(s => s.Id).ToList();
            var count = 0;

            foreach (var slotId in slots)
            {
                var occupied = false;
                for (var time = start; time < end; time = time.AddMinutes(StepMinutes))
                {
                    // Wahrscheinlichkeit, dass ein Fahrrad dasteht, hängt von der Uhrzeit ab
                    var target = OccupancyProbability(time.ToLocalTime(), slotId);
                    if (random.NextDouble() < 0.25)
                    {
                        occupied = random.NextDouble() < target;
                    }

                    // Seltene Manipulation: Rütteln, Druck springt, Abstand schwankt
                    var tamper = random.NextDouble() < 0.002;

                    var pressure = occupied
                        ? Noise(random, 850, 40)
                        : Noise(random, 40, 15);
                    var distance = occupied
                        ? Noise(random, 28, 2)
                        : Noise(random, 80, 3);

                    if (tamper)
                    {
                        pressure = Math.Clamp(pressure + random.Next(-600, 300), 0, 4095);
                        distance = Math.Clamp(distance + random.Next(-20, 40), 0, 1000);
                    }

                    db.SensorReadings.Add(new SensorReading
                    {
                        SlotId = slotId,
                        Pressure = pressure,
                        Distance = distance,
                        Vibration = tamper,
                        Occupied = pressure >= threshold,
                        Timestamp = time
                    });
                    count++;

                    if (tamper)
                    {
                        db.Alerts.Add(new Alert
                        {
                            SlotId = slotId,
                            Type = AlertType.PossibleTampering,
                            Severity = AlertSeverity.Warning,
                            Message = $"Mögliche Manipulation an Stellplatz {slotId} erkannt.",
                            Timestamp = time,
                            Resolved = true
                        });
                    }
                }
            }

            // Stellplätze auf den letzten erzeugten Messwert setzen, damit sie nicht "Keine Daten" zeigen
            foreach (var slot in db.Slots.ToList())
            {
                var last = db.SensorReadings.Local.Where(r => r.SlotId == slot.Id).MaxBy(r => r.Timestamp);
                if (last is null || (slot.LastUpdated ?? DateTime.MinValue) > last.Timestamp) continue;
                slot.Status = last.Occupied ? SlotStatus.Occupied : SlotStatus.Free;
                slot.LastUpdated = last.Timestamp;
            }

            await db.SaveChangesAsync();
            return count;
        }

        // Stoßzeiten morgens (7–9 Uhr) und nachmittags (15–18 Uhr), am Wochenende weniger
        private static double OccupancyProbability(DateTime local, int slotId)
        {
            var hour = local.Hour + local.Minute / 60.0;
            var weekend = local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            double p = hour switch
            {
                < 6 => 0.05,
                < 7 => 0.3,
                < 9.5 => 0.9,
                < 15 => 0.7,
                < 18 => 0.85,
                < 21 => 0.3,
                _ => 0.1
            };
            if (weekend) p *= 0.4;

            // Platz 1 (nah am Eingang) ist beliebter als Platz 3
            return Math.Clamp(p + (2 - slotId) * 0.05, 0, 1);
        }

        private static int Noise(Random random, int mean, int spread)
        {
            return Math.Max(0, mean + (int)Math.Round((random.NextDouble() * 2 - 1) * spread));
        }
    }
}
