namespace bikestation.Dtos
{
    // Anteil belegter Messungen je Stunde (Ortszeit des Servers)
    public record HourlyOccupancyDto(int Hour, double OccupancyPercent, int Readings);

    public record SlotStatisticsDto(
        int SlotId,
        string Name,
        double OccupancyPercent,
        int TamperEvents,
        int AnomalyEvents);

    public record StatisticsDto(
        int PeriodDays,
        int TotalReadings,
        double OccupancyPercent,
        int? BusiestHour,
        int TamperEvents,
        int AnomalyEvents,
        int OpenAlerts,
        List<HourlyOccupancyDto> OccupancyByHour,
        List<SlotStatisticsDto> Slots);
}
