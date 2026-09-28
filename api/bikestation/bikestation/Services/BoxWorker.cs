namespace bikestation.Services
{
    // Prüft regelmäßig die Zeitlimits offener Boxen (kein Fahrrad eingestellt / nicht abgeholt)
    public class BoxWorker(IServiceScopeFactory scopeFactory, ILogger<BoxWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<BoxService>().CheckTimeoutsAsync(DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Fehler beim Prüfen der Box-Zeitlimits");
                }
            }
        }
    }
}
