import type { Slot } from '../api'
import { displayStatus, type DisplayStatus } from '../slotStatus'

const lightClass: Record<DisplayStatus, string> = {
  free: 'is-available',
  taken: 'is-occupied',
  offline: 'is-unknown',
  unknown: 'is-unknown',
}

// Grafische Nachbildung der Station – rein dekorativ, die Textinfos stehen in der Liste darunter
export default function OccupancyFrame({ slots }: { slots: Slot[] }) {
  return (
    <div className="occupancy-frame" aria-hidden="true">
      <span className="frame-label">STATION</span>
      <div className="status-lights">
        {slots.map((slot) => (
          <span key={slot.id} className={`status-light ${lightClass[displayStatus(slot)]}`}>
            {slot.id}
          </span>
        ))}
      </div>
      <div className="frame-caption">
        <span>GRÜN = FREI</span>
        <span>ROT = BELEGT</span>
      </div>
    </div>
  )
}
