using bikestation.Models;

namespace bikestation.Dtos
{
    public record SensorReadingDto(int Pressure, int Distance, bool Vibration, bool Occupied, DateTime Timestamp);

    public record SlotDto(
        int Id,
        string Name,
        SlotStatus Status,
        string StatusText,
        DateTime? LastUpdated,
        bool IsOnline,
        bool PossibleTampering,
        bool HasAnomaly,
        SensorReadingDto? LatestReading);

    public record AlertDto(
        int Id,
        int SlotId,
        AlertType Type,
        AlertSeverity Severity,
        string Message,
        DateTime Timestamp,
        bool Resolved,
        double? Score);

    // Messwert mit Slot-ID, z. B. für den KI-Dienst
    public record SlotReadingDto(long Id, int SlotId, int Pressure, int Distance, bool Vibration, bool Occupied, DateTime Timestamp);

    public record SensorDataResponse(int SlotId, bool Occupied, SlotStatus Status);
}
