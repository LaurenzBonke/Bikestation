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
    """Klassischer Ultraschallsensor mit getrennten Trig- und Echo-Pins (Echo über Spannungsteiler auf 3,3 V!)."""

    def __init__(self, trigger_pin: int, echo_pin: int, timeout_s: float = 0.04):
        import lgpio
        self._lgpio = lgpio
        self._h = gpio_chip()
        self._trig, self._echo = trigger_pin, echo_pin
        self._timeout = timeout_s
        lgpio.gpio_claim_output(self._h, self._trig, 0)
        lgpio.gpio_claim_input(self._h, self._echo)

    def read_cm(self) -> int:
        lg, h = self._lgpio, self._h
        lg.gpio_write(h, self._trig, 1)
        _busy_wait_us(10)
        lg.gpio_write(h, self._trig, 0)
        return _measure_echo(lambda: lg.gpio_read(h, self._echo), self._timeout)


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
    """Servo als Riegel. Nach jeder Bewegung wird das PWM-Signal abgeschaltet (kein Zittern, weniger Strom)."""

    def __init__(self, pin: int, open_angle: float, closed_angle: float, move_time_s: float = 0.6):
        import lgpio
        self._lgpio = lgpio
        self._h = gpio_chip()
        self._pin = pin
        self._open_angle, self._closed_angle = open_angle, closed_angle
        self._move_time = move_time_s
        self.is_open: bool | None = None  # unbekannt bis zum ersten Befehl
        lgpio.gpio_claim_output(self._h, pin, 0)

    def set_open(self, open_: bool) -> bool:
        """Stellt den Riegel. Gibt True zurück, wenn sich etwas bewegt hat."""
        if self.is_open == open_:
            return False
        angle = self._open_angle if open_ else self._closed_angle
        pulse_us = 500 + (angle / 180.0) * 2000  # 0° = 0,5 ms, 180° = 2,5 ms
        self._lgpio.tx_servo(self._h, self._pin, int(pulse_us))
        time.sleep(self._move_time)
        self._lgpio.tx_servo(self._h, self._pin, 0)  # Signal aus
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
