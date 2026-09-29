import type { Slot } from '../api'
import { displayStatus, type DisplayStatus } from '../slotStatus'
import { useI18n } from '../i18n'

const lightClass: Record<DisplayStatus, string> = {
  free: 'is-available',
  taken: 'is-occupied',
  offline: 'is-unknown',
  unknown: 'is-unknown',
}

// Grafische Nachbildung der Station – rein dekorativ, die Textinfos stehen in der Liste darunter
export default function OccupancyFrame({ slots }: { slots: Slot[] }) {
  const { t } = useI18n()

  return (
    <div className="occupancy-frame" aria-hidden="true">
      <span className="frame-label">{t('frame.label')}</span>
      <div className="status-lights" style={{ gridTemplateColumns: `repeat(${Math.max(slots.length, 1)}, 1fr)` }}>
        {slots.map((slot) => (
          <span key={slot.id} className={`status-light ${lightClass[displayStatus(slot)]}`}>
            {slot.id}
          </span>
        ))}
      </div>
      <div className="frame-caption">
        <span>{t('frame.free')}</span>
        <span>{t('frame.taken')}</span>
      </div>
    </div>
  )
}
