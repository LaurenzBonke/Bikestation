using bikestation.Models;

namespace bikestation.Dtos
{
    public record SensorReadingDto(int Pressure, int Distance, bool Vibration, bool Occupied, DateTime Timestamp);

    public record SlotDto(
        int Id,
        string Name,
        string Location,
        SlotStatus Status,
        string StatusText,
        DateTime? LastUpdated,
        bool IsOnline,
        BoxState BoxState,
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

    // Messwert mit Slot-ID, z. B. für externe Auswertungen
    public record SlotReadingDto(long Id, int SlotId, int Pressure, int Distance, bool Vibration, bool Occupied, DateTime Timestamp);

    // Antwort an das Gerät: enthält direkt, ob der Riegel offen sein soll
    public record SensorDataResponse(int SlotId, bool Occupied, SlotStatus Status, BoxState BoxState, bool LockOpen);
}
