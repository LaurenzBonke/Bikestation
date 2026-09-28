using System.ComponentModel.DataAnnotations;

namespace bikestation.Dtos
{
    // Payload, den jeder ESP32 an POST /api/sensor-data schickt
    public class SensorDataRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int? SlotId { get; set; }

        // ESP32-ADC ist standardmäßig 12 Bit
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
