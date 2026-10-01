using System.Collections.Concurrent;
using bikestation.Data;
using bikestation.Dtos;
using bikestation.Models;
using Microsoft.EntityFrameworkCore;

namespace bikestation.Services
{
    // Ersetzt im Demo-Modus die echte Station (Raspberry Pi mit Ultraschallsensor, Servo-Riegel und zwei LEDs).
    // Meldet jede Sekunde für jede Box einen Abstand – wie pi/parking_sensor.py und über denselben
    // SensorDataService – und merkt sich die Antwort des Servers (Riegel auf/zu, Box-Zustand).
    // Ob ein Fahrrad in der Box steht, stellt man im Dashboard ein (POST /api/demo/station/{id}/bike).
    public class VirtualStation(IServiceScopeFactory scopeFactory, ILogger<VirtualStation> logger) : BackgroundService
    {
        public const int BikeDistanceCm = 3;
        public const int EmptyDistanceCm = 80;

        private readonly ConcurrentDictionary<int, bool> _bikes = new();
        private readonly ConcurrentDictionary<int, VirtualBoxDto> _boxes = new();
        private readonly Random _random = new();

        public IReadOnlyList<VirtualBoxDto> Boxes => _boxes.Values.OrderBy(b => b.SlotId).ToList();

        public bool HasBox(int slotId) => _boxes.ContainsKey(slotId);

        public void SetBike(int slotId, bool present)
        {
            _bikes[slotId] = present;
            logger.LogInformation("Demo: Fahrrad an Box {SlotId} {Action}", slotId, present ? "hineingestellt" : "herausgenommen");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Demo: virtuelle Station aktiv");
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            do
            {
                try
                {
                    await TickAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Fehler in der virtuellen Station");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        // Eine Messrunde über alle Boxen
        public async Task TickAsync()
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BikestationDbContext>();
            var sensorData = scope.ServiceProvider.GetRequiredService<SensorDataService>();

            var slotIds = await db.Slots.AsNoTracking().Select(s => s.Id).ToListAsync();
            foreach (var removed in _boxes.Keys.Except(slotIds))
            {
                _boxes.TryRemove(removed, out _);
            }

            foreach (var slotId in slotIds)
            {
                var bike = _bikes.GetValueOrDefault(slotId);
                // Etwas Rauschen wie beim echten Sensor
                var distance = bike ? BikeDistanceCm + _random.Next(-1, 2) : EmptyDistanceCm + _random.Next(-2, 3);
                var response = await sensorData.ProcessAsync(new SensorDataRequest
                {
                    SlotId = slotId,
                    Pressure = 0,  // wie am Pi: kein Drucksensor angeschlossen
                    Distance = distance,
                    Vibration = false
                });
                if (response is null) continue;

                // LEDs wie an der Station: grün aus, solange ein Fahrrad eingeschlossen oder die Box gesperrt ist;
                // rot an bei eingeschlossenem Fahrrad, blinkend nach unerlaubter Entnahme
                var ledGreen = response.BoxState is not (BoxState.Locked or BoxState.Blocked);
                var ledRed = response.BoxState switch
                {
                    BoxState.Locked => RedLed.On,
                    BoxState.Blocked => RedLed.Blinking,
                    _ => RedLed.Off
                };
                _boxes[slotId] = new VirtualBoxDto(slotId, bike, distance, response.LockOpen, ledGreen, ledRed, response.BoxState);
            }
        }
    }
}
