"""
Hardware-Zugriff für die Bikestation auf dem Raspberry Pi 5.

Nutzt lgpio (funktioniert auch auf dem Pi 5, RPi.GPIO nicht). Für Tests ohne Pi gibt es
Simulations-Klassen mit derselben Schnittstelle – der Agent merkt keinen Unterschied.
"""

from __future__ import annotations

import logging
import threading
import time

log = logging.getLogger("hardware")

NO_ECHO_CM = 500  # nichts im Messbereich


# ---------------------------------------------------------------- GPIO-Chip (echter Pi)

_chip = None
_chip_lock = threading.Lock()


def gpio_chip():
    """Öffnet den GPIO-Chip der Stiftleiste. Auf dem Pi 5 heißt er 'pinctrl-rp1' (gpiochip0 oder gpiochip4)."""
    global _chip
    with _chip_lock:
        if _chip is not None:
            return _chip
        import lgpio  # nur auf dem Pi vorhanden

        for number in (0, 4, 1, 2, 3):
            try:
                handle = lgpio.gpiochip_open(number)
            except lgpio.error:
                continue
            label = lgpio.gpio_get_chip_info(handle)[3]
            if "rp1" in label or "bcm" in label.lower() or number == 0:
                log.info("GPIO-Chip %d (%s)", number, label)
                _chip = handle
                return _chip
            lgpio.gpiochip_close(handle)
        raise RuntimeError("Kein GPIO-Chip gefunden – läuft das Programm auf dem Raspberry Pi?")


# ---------------------------------------------------------------- Ultraschall

class GroveUltrasonic:
    """Grove Ultrasonic Ranger V2.0: EIN Signal-Pin (SIG) für Trigger und Echo. NC bleibt unbelegt."""

    def __init__(self, pin: int, timeout_s: float = 0.04):
        import lgpio
        self._lgpio = lgpio
        self._h = gpio_chip()
        self._pin = pin
        self._timeout = timeout_s

    def read_cm(self) -> int:
        lg, h, pin = self._lgpio, self._h, self._pin
        lg.gpio_claim_output(h, pin, 0)
        time.sleep(0.000002)
        lg.gpio_write(h, pin, 1)
        _busy_wait_us(10)
        lg.gpio_write(h, pin, 0)
        lg.gpio_claim_input(h, pin)
        return _measure_echo(lambda: lg.gpio_read(h, pin), self._timeout)


class HcSr04Ultrasonic:
    """Ultraschallsensor mit getrennten Trig- und Echo-Pins – genau wie in parking_sensor.py über gpiozero.

    gpiozero misst im Hintergrund fortlaufend und mittelt; das ist stabiler als Einzelmessungen
    (die kamen bei diesem Sensor jedes zweite Mal ohne Echo zurück)."""

    def __init__(self, trigger_pin: int, echo_pin: int, max_distance_m: float = 1.0):
        from gpiozero import DistanceSensor
        from gpiozero.pins.lgpio import LGPIOFactory
        self._sensor = DistanceSensor(echo=echo_pin, trigger=trigger_pin, max_distance=max_distance_m,
                                      pin_factory=LGPIOFactory())
        self._max_cm = int(max_distance_m * 100)
        time.sleep(0.5)  # erste Messungen abwarten

    def read_cm(self) -> int:
        cm = int(round(self._sensor.distance * 100))
        # gpiozero liefert bei "nichts in Reichweite" max_distance
        return NO_ECHO_CM if cm >= self._max_cm else cm


def _busy_wait_us(us: float) -> None:
    end = time.perf_counter_ns() + int(us * 1000)
    while time.perf_counter_ns() < end:
        pass


def _measure_echo(read, timeout_s: float) -> int:
    """Misst die Länge des HIGH-Pulses. Kein Puls oder zu lang -> NO_ECHO_CM."""
    deadline = time.perf_counter_ns() + int(timeout_s * 1e9)
    while read() == 0:
        if time.perf_counter_ns() > deadline:
            return NO_ECHO_CM
    start = time.perf_counter_ns()
    while read() == 1:
        if time.perf_counter_ns() > deadline:
            return NO_ECHO_CM
    pulse_us = (time.perf_counter_ns() - start) / 1000
    cm = int(pulse_us / 29 / 2)  # Schall: ca. 29 µs pro cm, hin und zurück
    return cm if cm <= 350 else NO_ECHO_CM


def median_distance(sensor, samples: int = 3, pause_s: float = 0.02) -> int:
    """Median mehrerer Messungen – einzelne Ausreißer fallen weg."""
    values = []
    for i in range(samples):
        values.append(sensor.read_cm())
        if i < samples - 1:
            time.sleep(pause_s)
    return sorted(values)[len(values) // 2]


# ---------------------------------------------------------------- Servo-Riegel

class ServoLock:
    """Servo als Riegel – wie in parking_sensor.py: gpiozero AngularServo, 0,5–2,5 ms Pulsbreite.
    Nach jeder Bewegung wird das Signal abgeschaltet (kein Zittern, weniger Strom)."""

    def __init__(self, pin: int, open_angle: float, closed_angle: float, move_time_s: float = 0.6):
        from gpiozero import AngularServo
        from gpiozero.pins.lgpio import LGPIOFactory
        self._servo = AngularServo(pin, min_angle=0, max_angle=180,
                                   min_pulse_width=0.5 / 1000, max_pulse_width=2.5 / 1000,
                                   pin_factory=LGPIOFactory())
        self._servo.detach()
        self._open_angle, self._closed_angle = open_angle, closed_angle
        self._move_time = move_time_s
        self.is_open: bool | None = None  # unbekannt bis zum ersten Befehl

    def set_open(self, open_: bool) -> bool:
        """Stellt den Riegel. Gibt True zurück, wenn sich etwas bewegt hat."""
        if self.is_open == open_:
            return False
        self._servo.angle = self._open_angle if open_ else self._closed_angle
        time.sleep(self._move_time)
        self._servo.detach()
        self.is_open = open_
        return True


# ---------------------------------------------------------------- Vibration

class VibrationSensor:
    """Digitaler Vibrationssensor. Jede Flanke seit der letzten Abfrage zählt als Vibration."""

    def __init__(self, pin: int, active_high: bool = True):
        import lgpio
        self._lgpio = lgpio
        self._h = gpio_chip()
        self._pin = pin
        self._active_high = active_high
        self._hits = 0
        lgpio.gpio_claim_alert(self._h, pin, lgpio.RISING_EDGE if active_high else lgpio.FALLING_EDGE)
        self._cb = lgpio.callback(self._h, pin, lgpio.RISING_EDGE if active_high else lgpio.FALLING_EDGE, self._on_edge)

    def _on_edge(self, *_):
        self._hits += 1

    def consume(self) -> bool:
        hit, self._hits = self._hits > 0, 0
        return hit


# ---------------------------------------------------------------- Simulation (ohne Pi)

class SimulatedBox:
    """Ersetzt Sensoren und Servo einer Box. Abstand und Vibration lassen sich von außen setzen."""

    def __init__(self):
        self.distance_cm = 80
        self.vibration = False
        self.is_open: bool | None = None
        self.moves: list[bool] = []

    # Ultraschall
    def read_cm(self) -> int:
        return self.distance_cm

    # Vibration
    def consume(self) -> bool:
        hit, self.vibration = self.vibration, False
        return hit

    # Servo
    def set_open(self, open_: bool) -> bool:
        if self.is_open == open_:
            return False
        self.is_open = open_
        self.moves.append(open_)
        return True
