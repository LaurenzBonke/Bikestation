namespace bikestation.Dtos
{
    // Erwartete Belegung für eine kommende Stunde. ExpectedFree ist null, wenn es zu wenig Daten gibt.
    public record ForecastHourDto(DateTime Start, int Hour, double? OccupancyPercent, double? ExpectedFree, int Readings);

    public record ForecastDto(int TotalSlots, int BasedOnDays, List<ForecastHourDto> Hours);
}
