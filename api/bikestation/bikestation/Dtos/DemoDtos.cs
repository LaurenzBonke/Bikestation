using bikestation.Models;

namespace bikestation.Dtos
{
    // Was das Dashboard über den Demo-Modus wissen muss
    public record DemoInfo(bool VirtualStation, IReadOnlyList<DemoAccountDto> Accounts);

    public record DemoAccountDto(string Username, string Password, UserRole Role);

    // Zustand einer Box der virtuellen Station – so, wie ihn der Pi mit Ultraschall, Servo und LEDs hätte
    public record VirtualBoxDto(int SlotId, bool BikePresent, int DistanceCm, bool LockOpen, bool LedGreen, RedLed LedRed, BoxState BoxState);

    // Rote LED der Station: an, solange ein Fahrrad eingeschlossen ist, blinkt nach unerlaubter Entnahme
    public enum RedLed
    {
        Off,
        On,
        Blinking
    }

    public class VirtualBikeRequest
    {
        public bool Present { get; set; }
    }
}
