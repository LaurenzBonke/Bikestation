import type { Slot } from '../api'
import { formatTime, pad2 } from '../format'

const statusClass: Record<Slot['status'], string> = {
  Free: 'free',
  Occupied: 'taken',
  Unknown: 'unknown',
}

export default function SlotList({ slots }: { slots: Slot[] }) {
  return (
    <>
      <div className="spots-heading">
        <div>
          <h3 className="section-kicker">STELLPLÄTZE</h3>
          <span className="spots-hint">Status und Sensorwerte je Platz</span>
        </div>
        <span className="total-spots">{pad2(slots.length)} GESAMT</span>
      </div>

      <ul className="spot-list">
        {slots.map((slot) => (
          <li key={slot.id} className={`spot-row ${slot.possibleTampering ? 'has-warning' : ''}`}>
            <span className="spot-number" aria-hidden="true">
              {pad2(slot.id)}
            </span>
            <span className="spot-bike" aria-hidden="true">
              <svg viewBox="0 0 32 24" fill="none">
                <circle cx="7" cy="16" r="5" />
                <circle cx="25" cy="16" r="5" />
                <path d="m7 16 6-10 6 10H7Zm6-10h5m-2 0 9 10" />
              </svg>
            </span>
            <span className="spot-main">
              <span className="spot-name">{slot.name}</span>
              <SensorValues slot={slot} />
            </span>
            <span className={`spot-status ${statusClass[slot.status]}`}>
              <span aria-hidden="true"></span>
              {slot.status === 'Unknown' ? 'Keine Daten' : slot.statusText}
            </span>
            {slot.possibleTampering && (
              <p className="tamper-warning" role="alert">
                <span aria-hidden="true">⚠</span> Mögliche Manipulation erkannt
              </p>
            )}
          </li>
        ))}
      </ul>
    </>
  )
}

function SensorValues({ slot }: { slot: Slot }) {
  const reading = slot.latestReading
  if (!reading) {
    return <span className="sensor-values">Noch keine Sensordaten empfangen</span>
  }

  return (
    <span className="sensor-values">
      Druck {reading.pressure} · Abstand {reading.distance} cm · Vibration {reading.vibration ? 'ja' : 'nein'} ·{' '}
      {formatTime(reading.timestamp)}
    </span>
  )
}
