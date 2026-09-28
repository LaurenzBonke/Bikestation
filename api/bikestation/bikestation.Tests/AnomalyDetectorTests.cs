using bikestation.Models;
using bikestation.Services;

namespace bikestation.Tests
{
    // Prüft die statistische Anomalieerkennung ohne API und Datenbank
    public class AnomalyDetectorTests
    {
        private const int Slot = 1;

        // Normale Historie: abwechselnd längere Phasen frei und belegt, mit leichtem Rauschen
        private static List<SensorReading> NormalHistory()
        {
            var random = new Random(7);
            var history = new List<SensorReading>();
            for (var i = 0; i < 600; i++)
            {
                var occupied = i / 50 % 2 == 0;
                history.Add(Reading(
                    occupied ? 850 + random.Next(-25, 26) : 40 + random.Next(-10, 11),
                    occupied ? 28 + random.Next(-2, 3) : 80 + random.Next(-2, 3),
                    occupied));
            }
            return history;
        }

        private static SensorReading Reading(int pressure, int distance, bool occupied, bool vibration = false) =>
            new() { SlotId = Slot, Pressure = pressure, Distance = distance, Occupied = occupied, Vibration = vibration };

        private static AnomalyDetector TrainedDetector()
        {
            var detector = new AnomalyDetector(zThreshold: 6, minSamples: 30);
            detector.Train(Slot, NormalHistory());
            return detector;
        }

        [Fact]
        public void Normale_Messung_ist_keine_Anomalie()
        {
            var result = TrainedDetector().Evaluate(Reading(845, 28, true), Reading(852, 29, true));
            Assert.False(result.IsAnomaly);
        }

        [Fact]
        public void Fahrrad_kommt_an_ist_keine_Anomalie()
        {
            // Wechsel frei -> belegt ist normal, auch wenn sich der Druck stark ändert
            var result = TrainedDetector().Evaluate(Reading(840, 29, true), Reading(38, 81, false));
            Assert.False(result.IsAnomaly);
        }

        [Fact]
        public void Vibration_allein_ist_keine_Anomalie()
        {
            var result = TrainedDetector().Evaluate(Reading(848, 28, true, vibration: true), Reading(850, 28, true));
            Assert.False(result.IsAnomaly);
        }

        [Fact]
        public void Ruetteln_mit_Druckabfall_ist_eine_Anomalie()
        {
            var result = TrainedDetector().Evaluate(Reading(560, 41, true, vibration: true), Reading(850, 28, true));
            Assert.True(result.IsAnomaly);
            Assert.True(result.Score >= 0.8);
            Assert.StartsWith("Vibration", result.Reason);
        }

        [Fact]
        public void Widerspruechliche_Sensoren_sind_eine_Anomalie()
        {
            // Druck sagt "belegt", Ultraschall sieht aber nichts im Stellplatz
            var result = TrainedDetector().Evaluate(Reading(860, 80, true), Reading(855, 28, true));
            Assert.True(result.IsAnomaly);
            Assert.Contains("Abstand", result.Reason);
        }

        [Fact]
        public void Ohne_genug_Daten_wird_nicht_bewertet()
        {
            var detector = new AnomalyDetector(zThreshold: 6, minSamples: 30);
            detector.Train(Slot, NormalHistory().Take(10).ToList());
            Assert.False(detector.Evaluate(Reading(10, 300, true), null).IsAnomaly);
        }
    }
}
