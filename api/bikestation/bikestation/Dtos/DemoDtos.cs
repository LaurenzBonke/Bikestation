using bikestation.Models;

namespace bikestation.Dtos
{
    // Was das Dashboard über den Demo-Modus wissen muss
    public record DemoInfo(bool VirtualStation, IReadOnlyList<DemoAccountDto> Accounts);

    public record DemoAccountDto(string Username, string Password, UserRole Role);

    // Zustand einer Box der virtuellen Station – so, wie ihn der Pi mit Ultraschall, Servo und LED hätte
    public record VirtualBoxDto(int SlotId, bool BikePresent, int DistanceCm, bool LockOpen, bool LedGreen, BoxState BoxState);

    public class VirtualBikeRequest
    {
        public bool Present { get; set; }
    }
}
