using bikestation.Models;

namespace bikestation.Dtos
{
    // Öffentliche Sicht auf eine Box – ohne Hinweis, wem sie gehört (nur "IsMine" für den Angemeldeten)
    public record BoxDto(
        int Id,
        string Name,
        BoxState State,
        bool LockOpen,
        bool BikeDetected,
        bool IsOnline,
        bool IsMine,
        DateTime? StateChangedAt);

    // Eintrag im Box-Protokoll (nur für Admins)
    public record BoxEventDto(
        int Id,
        int SlotId,
        string SlotName,
        string? Username,
        BoxEventType Type,
        bool LockOpen,
        DateTime Timestamp);

    // Laufender Parkvorgang des angemeldeten Nutzers
    public record MyParkingDto(
        int ParkingId,
        int SlotId,
        BoxState State,
        bool LockOpen,
        bool IsOnline,
        DateTime BookedAt,
        DateTime? ParkedAt,
        DateTime? PickupRequestedAt,
        // Nur in offenen Zuständen: bis wann das Fahrrad eingestellt bzw. entnommen sein muss
        DateTime? DeadlineAt);

    public record ParkingHistoryDto(int SlotId, DateTime BookedAt, DateTime? ParkedAt, DateTime? EndedAt, ParkingEndReason? EndReason);

    public record MeDto(
        int Id,
        string Username,
        UserRole Role,
        MyParkingDto? Parking,
        List<AlertDto> Alerts,
        List<ParkingHistoryDto> History);

    // Für den Pi/ESP32: soll der Riegel einer Box offen sein?
    public record DeviceBoxDto(int SlotId, BoxState State, bool LockOpen);
}
