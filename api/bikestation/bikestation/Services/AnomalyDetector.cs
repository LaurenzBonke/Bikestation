using bikestation.Models;

namespace bikestation.Services
{
    // Statistische Anomalieerkennung (robuster Z-Score mit Median und MAD).
    //
    // Das Modell lernt pro Stellplatz aus den bisherigen Messwerten, wie Druck und Abstand im Zustand
    // "frei" bzw. "belegt" normalerweise aussehen und wie stark sie sich zwischen zwei Messungen ändern.
    // Ein neuer Messwert ist auffällig, wenn er stark von diesem gelernten Normalbereich abweicht –
    // z. B. Druck fällt stark bei stehendem Fahrrad, oder Druck sagt "belegt", der Abstand aber "leer".
    // Vibration allein löst keine Anomalie aus, erhöht aber den Score, wenn zusätzlich Werte abweichen.
    public class AnomalyDetector(double zThreshold, int minSamples)
    {
        // Faktor, damit MAD bei normalverteilten Daten der Standardabweichung entspricht
        private const double MadScale = 1.4826;
        // Untergrenze der Streuung, damit sehr gleichmäßige Werte nicht jede Kleinigkeit melden
        private const double MinSpread = 3;

        private static readonly Dictionary<string, string> FeatureText = new()
        {
            ["pressure"] = "ungewöhnlicher Druck",
            ["distance"] = "ungewöhnlicher Abstand",
            ["pressureChange"] = "starke Druckänderung",
            ["distanceChange"] = "starke Abstandsänderung",
        };

        public record Result(bool IsAnomaly, double Score, double MaxZ, string Reason);

        private record Baseline(double Median, double Spread, int Count);

        private readonly Dictionary<(int Slot, bool Occupied, string Feature), Baseline> _baselines = [];

        public bool IsTrained(int slotId, bool occupied) =>
            _baselines.TryGetValue((slotId, occupied, "pressure"), out var b) && b.Count >= minSamples;

        // Lernt aus der Historie eines Stellplatzes (älteste zuerst)
        public void Train(int slotId, IReadOnlyList<SensorReading> history)
        {
            foreach (var occupied in new[] { true, false })
            {
                var inState = history.Where(r => r.Occupied == occupied).ToList();
                _baselines[(slotId, occupied, "pressure")] = Fit(inState.Select(r => (double)r.Pressure));
                _baselines[(slotId, occupied, "distance")] = Fit(inState.Select(r => (double)r.Distance));

                // Änderungen nur zwischen zwei Messungen im selben Zustand – Kommen/Gehen ist normal
                var changes = history.Zip(history.Skip(1))
                    .Where(p => p.First.Occupied == occupied && p.Second.Occupied == occupied)
                    .ToList();
                _baselines[(slotId, occupied, "pressureChange")] =
                    Fit(changes.Select(p => (double)Math.Abs(p.Second.Pressure - p.First.Pressure)));
                _baselines[(slotId, occupied, "distanceChange")] =
                    Fit(changes.Select(p => (double)Math.Abs(p.Second.Distance - p.First.Distance)));
            }
        }

        public Result Evaluate(SensorReading reading, SensorReading? previous)
        {
            if (!IsTrained(reading.SlotId, reading.Occupied))
            {
                return new Result(false, 0, 0, "zu wenig Daten");
            }

            var z = new Dictionary<string, double>
            {
                ["pressure"] = Z(reading, "pressure", reading.Pressure),
                ["distance"] = Z(reading, "distance", reading.Distance),
            };
            if (previous is not null && previous.Occupied == reading.Occupied)
            {
                z["pressureChange"] = Z(reading, "pressureChange", Math.Abs(reading.Pressure - previous.Pressure));
                z["distanceChange"] = Z(reading, "distanceChange", Math.Abs(reading.Distance - previous.Distance));
            }

            var maxZ = z.Values.Max();
            // Score 0..1: z = 3 -> 0.5, z = 9 -> 0.75. Vibration verstärkt nur vorhandene Abweichungen.
            var score = maxZ / (maxZ + 3);
            var threshold = zThreshold;
            if (reading.Vibration)
            {
                score = Math.Min(1, score + 0.15);
                threshold *= 0.6;
            }

            var reasons = z.Where(kv => kv.Value >= threshold * 0.5)
                .OrderByDescending(kv => kv.Value)
                .Take(2)
                .Select(kv => FeatureText[kv.Key])
                .ToList();
            if (reading.Vibration) reasons.Insert(0, "Vibration");

            var isAnomaly = maxZ >= threshold;
            return new Result(isAnomaly, Math.Round(score, 3), Math.Round(maxZ, 1),
                reasons.Count > 0 ? string.Join(" + ", reasons) : "ungewöhnliche Kombination der Messwerte");
        }

        private double Z(SensorReading reading, string feature, double value)
        {
            var b = _baselines[(reading.SlotId, reading.Occupied, feature)];
            return b.Count == 0 ? 0 : Math.Abs(value - b.Median) / b.Spread;
        }

        private static Baseline Fit(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            if (sorted.Length == 0) return new Baseline(0, MinSpread, 0);

            var median = Median(sorted);
            var mad = Median(sorted.Select(v => Math.Abs(v - median)).OrderBy(v => v).ToArray());
            return new Baseline(median, Math.Max(mad * MadScale, MinSpread), sorted.Length);
        }

        private static double Median(double[] sorted)
        {
            var mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        }
    }
}
