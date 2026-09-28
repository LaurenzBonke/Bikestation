import type { Slot } from '../api'

const lightClass: Record<Slot['status'], string> = {
  Free: 'is-available',
  Occupied: 'is-occupied',
  Unknown: 'is-unknown',
}

// Grafische Nachbildung der Station – rein dekorativ, die Textinfos stehen in der Liste darunter
export default function OccupancyFrame({ slots }: { slots: Slot[] }) {
  return (
    <div className="occupancy-frame" aria-hidden="true">
      <span className="frame-label">STATION</span>
      <div className="status-lights">
        {slots.map((slot) => (
          <span key={slot.id} className={`status-light ${lightClass[slot.status]}`}>
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
