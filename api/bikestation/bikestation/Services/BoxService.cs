using bikestation.Data;
using bikestation.Models;
using bikestation.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bikestation.Services
{
    public enum BoxActionResult
    {
        Ok,
        NotFound,
        NotAllowed,       // Box gehört einem anderen Nutzer
        WrongState,       // Aktion passt nicht zum aktuellen Zustand
        AlreadyHasBox     // Nutzer hat bereits eine aktive Box
    }

    // Zustandsmaschine der abschließbaren Boxen:
    //   Free --Buchen--> OpenForParking --Fahrrad erkannt--> Locked --Abholen--> OpenForPickup --Fahrrad weg--> Free
    //   OpenForParking --Zeitlimit/Abbrechen--> Free,  OpenForPickup --Zeitlimit, Rad noch da--> Locked
    //   Locked --Fahrrad weg ohne Öffnen--> Blocked (Alarm an Nutzer + Admin) --Admin gibt frei--> Free
    public class BoxService(
        BikestationDbContext db,
        IOptions<BoxOptions> options,
        ILogger<BoxService> logger)
    {
        // Alle Zustandswechsel nacheinander, damit z. B. zwei Nutzer nicht gleichzeitig dieselbe Box buchen
        private static readonly SemaphoreSlim Gate = new(1, 1);

        private readonly BoxOptions _options = options.Value;

        public bool IsBikePresent(int distanceCm) => distanceCm <= _options.BikePresentMaxDistanceCm;

        public static async Task<T> LockedAsync<T>(Func<Task<T>> action)
        {
            await Gate.WaitAsync();
            try { return await action(); }
            finally { Gate.Release(); }
        }

        // ---------- Aktionen der Nutzer ----------

        public Task<BoxActionResult> BookAsync(int userId, int slotId) => LockedAsync(async () =>
        {
            var slot = await db.Slots.FindAsync(slotId);
            if (slot is null) return BoxActionResult.NotFound;
            if (await ActiveParkingOfUserAsync(userId) is not null) return BoxActionResult.AlreadyHasBox;
            if (slot.BoxState != BoxState.Free) return BoxActionResult.WrongState;

            var now = DateTime.UtcNow;
            var parking = new Parking { SlotId = slot.Id, UserId = userId, BookedAt = now };
            db.Parkings.Add(parking);
            await db.SaveChangesAsync();

            slot.ActiveParkingId = parking.Id;
            SetState(slot, BoxState.OpenForParking, now);
            await db.SaveChangesAsync();
            logger.LogInformation("Box {SlotId} von Nutzer {UserId} gebucht, Riegel öffnet", slot.Id, userId);
            return BoxActionResult.Ok;
        });

        public Task<BoxActionResult> CancelAsync(int userId, int slotId) => LockedAsync(async () =>
        {
            var (slot, parking, result) = await OwnedBoxAsync(userId, slotId);
            if (result != BoxActionResult.Ok) return result;
            if (slot!.BoxState != BoxState.OpenForParking) return BoxActionResult.WrongState;

            EndParking(slot, parking!, ParkingEndReason.Cancelled, DateTime.UtcNow);
            await db.SaveChangesAsync();
            return BoxActionResult.Ok;
        });

        public Task<BoxActionResult> RequestPickupAsync(int userId, int slotId) => LockedAsync(async () =>
        {
            var (slot, parking, result) = await OwnedBoxAsync(userId, slotId);
            if (result != BoxActionResult.Ok) return result;
            if (slot!.BoxState != BoxState.Locked) return BoxActionResult.WrongState;

            var now = DateTime.UtcNow;
            parking!.PickupRequestedAt = now;
            SetState(slot, BoxState.OpenForPickup, now);
            await db.SaveChangesAsync();
            logger.LogInformation("Box {SlotId}: Abholung angefordert, Riegel öffnet", slot.Id);
            return BoxActionResult.Ok;
        });

        // Admin gibt eine nach Alarm gesperrte Box nach der Kontrolle wieder frei
        public Task<BoxActionResult> ReleaseAsync(int slotId) => LockedAsync(async () =>
        {
            var slot = await db.Slots.FindAsync(slotId);
            if (slot is null) return BoxActionResult.NotFound;
            if (slot.BoxState != BoxState.Blocked) return BoxActionResult.WrongState;

            SetState(slot, BoxState.Free, DateTime.UtcNow);
            await db.SaveChangesAsync();
            return BoxActionResult.Ok;
        });

        // ---------- Sensordaten und Zeitlimits ----------

        // Wird bei jeder Messung aufgerufen (vor SaveChanges des Aufrufers)
        public async Task OnReadingAsync(Slot slot, int distanceCm, DateTime now)
        {
            var present = IsBikePresent(distanceCm);
            if (present)
            {
                slot.BikePresentSince ??= now;
                slot.BikeAbsentSince = null;
            }
            else
            {
                slot.BikeAbsentSince ??= now;
                slot.BikePresentSince = null;
            }

            var parking = slot.ActiveParkingId is int id ? await db.Parkings.FindAsync(id) : null;
            switch (slot.BoxState)
            {
                case BoxState.OpenForParking when HeldFor(SinceState(slot, slot.BikePresentSince), now, _options.ParkConfirmSeconds):
                    if (parking is not null) parking.ParkedAt = now;
                    SetState(slot, BoxState.Locked, now);
                    logger.LogInformation("Box {SlotId}: Fahrrad erkannt, Riegel schließt", slot.Id);
                    break;

                case BoxState.OpenForPickup when HeldFor(SinceState(slot, slot.BikeAbsentSince), now, _options.LeaveConfirmSeconds):
                    if (parking is not null) EndParking(slot, parking, ParkingEndReason.Completed, now);
                    else SetState(slot, BoxState.Free, now);
                    logger.LogInformation("Box {SlotId}: Fahrrad abgeholt, Box wieder frei", slot.Id);
                    break;

                case BoxState.Locked when HeldFor(SinceState(slot, slot.BikeAbsentSince), now, _options.AlarmConfirmSeconds):
                    RaiseBikeRemovedAlarm(slot, parking, now);
                    break;
            }
        }

        // Wird regelmäßig vom BoxWorker aufgerufen
        public Task<int> CheckTimeoutsAsync(DateTime now) => LockedAsync(async () =>
        {
            var changed = 0;
            var openSlots = await db.Slots
                .Where(s => s.BoxState == BoxState.OpenForParking || s.BoxState == BoxState.OpenForPickup)
                .ToListAsync();
            foreach (var slot in openSlots)
            {
                var since = slot.BoxStateChangedAt ?? now;
                var parking = slot.ActiveParkingId is int id ? await db.Parkings.FindAsync(id) : null;

                if (slot.BoxState == BoxState.OpenForParking && HeldFor(since, now, _options.OpenForParkingTimeoutSeconds))
                {
                    if (parking is not null) EndParking(slot, parking, ParkingEndReason.TimedOut, now);
                    else SetState(slot, BoxState.Free, now);
                    logger.LogInformation("Box {SlotId}: kein Fahrrad eingestellt, wieder frei", slot.Id);
                    changed++;
                }
                else if (slot.BoxState == BoxState.OpenForPickup && HeldFor(since, now, _options.OpenForPickupTimeoutSeconds))
                {
                    SetState(slot, BoxState.Locked, now);
                    logger.LogInformation("Box {SlotId}: Fahrrad nicht entnommen, wieder verriegelt", slot.Id);
                    changed++;
                }
            }
            if (changed > 0) await db.SaveChangesAsync();
            return changed;
        });

        // ---------- Hilfen ----------

        public async Task<Parking?> ActiveParkingOfUserAsync(int userId) =>
            await db.Parkings.Where(p => p.UserId == userId && p.EndedAt == null).OrderByDescending(p => p.Id).FirstOrDefaultAsync();

        // Besitzer der Box, solange ein Parkvorgang läuft – für Meldungen an den richtigen Nutzer
        public async Task<int?> OwnerOfAsync(Slot slot)
        {
            if (slot.ActiveParkingId is not int id) return null;
            return (await db.Parkings.FindAsync(id))?.UserId;
        }

        private async Task<(Slot?, Parking?, BoxActionResult)> OwnedBoxAsync(int userId, int slotId)
        {
            var slot = await db.Slots.FindAsync(slotId);
            if (slot is null) return (null, null, BoxActionResult.NotFound);
            var parking = slot.ActiveParkingId is int id ? await db.Parkings.FindAsync(id) : null;
            if (parking is null || parking.UserId != userId) return (slot, null, BoxActionResult.NotAllowed);
            return (slot, parking, BoxActionResult.Ok);
        }

        private void RaiseBikeRemovedAlarm(Slot slot, Parking? parking, DateTime now)
        {
            db.Alerts.Add(new Alert
            {
                SlotId = slot.Id,
                UserId = parking?.UserId,
                Type = AlertType.BikeRemoved,
                Severity = AlertSeverity.Critical,
                Message = $"Fahrrad wurde unerwartet aus {slot.Name} entfernt, ohne dass die Box geöffnet wurde.",
                Timestamp = now
            });
            if (parking is not null)
            {
                parking.EndedAt = now;
                parking.EndReason = ParkingEndReason.BikeRemoved;
            }
            slot.ActiveParkingId = null;
            SetState(slot, BoxState.Blocked, now);
            logger.LogWarning("ALARM: Fahrrad aus Box {SlotId} ohne Öffnen entfernt", slot.Id);
        }

        private static void EndParking(Slot slot, Parking parking, ParkingEndReason reason, DateTime now)
        {
            parking.EndedAt = now;
            parking.EndReason = reason;
            slot.ActiveParkingId = null;
            SetState(slot, BoxState.Free, now);
        }

        private static void SetState(Slot slot, BoxState state, DateTime now)
        {
            slot.BoxState = state;
            slot.BoxStateChangedAt = now;
        }

        // Wartezeiten zählen frühestens ab dem letzten Zustandswechsel – steht z. B. schon vor dem Buchen
        // etwas vor dem Sensor, verriegelt die Box nicht sofort
        private static DateTime? SinceState(Slot slot, DateTime? since) =>
            since is null ? null : slot.BoxStateChangedAt is DateTime changed && changed > since ? changed : since;

        private static bool HeldFor(DateTime? since, DateTime now, int seconds) =>
            since is not null && (now - since.Value).TotalSeconds >= seconds;
    }
}
