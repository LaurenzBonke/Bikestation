using System.ComponentModel.DataAnnotations;

namespace bikestation.Dtos
{
    // Payload, den die Station an POST /api/sensor-data schickt
    public class SensorDataRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int? SlotId { get; set; }

        // Optionaler Drucksensor (12-Bit-Rohwert), beim Hackathon nicht angeschlossen
        [Required]
        [Range(0, 4095)]
        public int? Pressure { get; set; }

        // Großzügiger Bereich in cm – genaue Sensorgrenzen noch nicht festgelegt
        [Required]
        [Range(0, 1000)]
        public int? Distance { get; set; }

        [Required]
        public bool? Vibration { get; set; }
    }
}
