using System.ComponentModel.DataAnnotations;

namespace bikestation.Dtos
{
    // Meldung einer Anomalie für POST /api/anomalies (die eingebaute Erkennung nutzt denselben Weg)
    public class AnomalyRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int? SlotId { get; set; }

        // 0 = normal, 1 = sehr ungewöhnlich
        [Required]
        [Range(0.0, 1.0)]
        public double? Score { get; set; }

        // Kurze Begründung, z. B. "Druck fällt stark bei gleichzeitiger Vibration"
        [StringLength(300)]
        public string? Reason { get; set; }
    }

    public record AnomalyResponse(bool Created, int? AlertId);
}
