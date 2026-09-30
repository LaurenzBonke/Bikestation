import { useState } from 'react'
import { api, type VirtualBox } from '../api'
import { useI18n, type TranslationKey } from '../i18n'
import { slotName } from '../slotStatus'
import { useVirtualStation } from '../useDemo'

// Demo-Modus: ersetzt die echte Station (Raspberry Pi mit Ultraschall, Servo-Riegel und grüner LED).
// Der Server steuert Riegel und LED genauso wie beim Pi – hier sieht man es und stellt das Fahrrad hinein.
export default function DemoStation({ onChange }: { onChange: () => void }) {
  const { t } = useI18n()
  const { boxes, failed, refresh } = useVirtualStation()
  const [busy, setBusy] = useState<number | null>(null)

  async function setBike(slotId: number, present: boolean) {
    setBusy(slotId)
    try {
      await api.setVirtualBike(slotId, present)
    } finally {
      setBusy(null)
      refresh()
      onChange()
    }
  }

  return (
    <section className="admin-panel admin-section demo-station" aria-labelledby="demo-heading">
      <p className="section-kicker">{t('demo.kicker')}</p>
      <h2 id="demo-heading">{t('demo.heading')}</h2>
      <p className="chart-note">{t('demo.intro')}</p>
      {failed && (
        <p className="error-notice" role="status">
          {t('demo.unavailable')}
        </p>
      )}
      <ul className="demo-grid">
        {boxes.map((box) => (
          <VirtualBoxCard key={box.slotId} box={box} busy={busy === box.slotId} onSetBike={(present) => setBike(box.slotId, present)} />
        ))}
      </ul>
    </section>
  )
}

function hintKey(box: VirtualBox): TranslationKey {
  switch (box.boxState) {
    case 'Free':
      return 'demo.hint.Free'
    case 'OpenForParking':
      return box.bikePresent ? 'demo.hint.detected' : 'demo.hint.OpenForParking'
    case 'Locked':
      return 'demo.hint.Locked'
    case 'OpenForPickup':
      return box.bikePresent ? 'demo.hint.OpenForPickup' : 'demo.hint.leaving'
    case 'Blocked':
      return 'demo.hint.Blocked'
  }
}

function VirtualBoxCard({ box, busy, onSetBike }: { box: VirtualBox; busy: boolean; onSetBike: (present: boolean) => void }) {
  const { t } = useI18n()
  const name = slotName(t, box.slotId)
  // Hineinstellen geht nur bei offenem Riegel; herausnehmen immer – bei verriegelter Box ist das ein Diebstahl
  const theft = box.bikePresent && !box.lockOpen
  const action: TranslationKey = !box.bikePresent ? 'demo.putIn' : theft ? 'demo.steal' : 'demo.takeOut'
  const disabled = busy || (!box.bikePresent && !box.lockOpen)

  return (
    <li className="demo-card">
      <h3>{name}</h3>
      <BoxDrawing box={box} label={t('demo.drawingAria', { slot: name })} />
      <ul className="demo-facts">
        <li>
          <span className={`demo-dot ${box.lockOpen ? 'is-open' : ''}`} aria-hidden="true"></span>
          {box.lockOpen ? t('demo.latchOpen') : t('demo.latchClosed')}
        </li>
        <li>
          <span className={`demo-dot led ${box.ledGreen ? 'is-on' : ''}`} aria-hidden="true"></span>
          {box.ledGreen ? t('demo.ledOn') : t('demo.ledOff')}
        </li>
        <li>
          <span className="demo-dot sensor" aria-hidden="true"></span>
          {t('demo.distance', { cm: box.distanceCm })}
        </li>
      </ul>
      <p className="demo-hint" aria-live="polite">
        <strong>{t(`state.${box.boxState}`)}:</strong> {t(hintKey(box))}
      </p>
      <button
        className={theft ? 'secondary-button danger-button' : 'secondary-button'}
        type="button"
        disabled={disabled}
        onClick={() => onSetBike(!box.bikePresent)}
        aria-label={t('demo.actionAria', { action: t(action), slot: name })}
      >
        {t(action)}
      </button>
    </li>
  )
}

// Seitenansicht der Box: Ultraschallsensor links, Servo-Riegel an der Öffnung rechts, LED oben
function BoxDrawing({ box, label }: { box: VirtualBox; label: string }) {
  return (
    <svg className="demo-drawing" viewBox="0 0 180 120" role="img" aria-label={label}>
      <rect className="demo-box" x="16" y="24" width="130" height="80" rx="6" />
      {/* Ultraschallsensor und Messstrahl bis zum Fahrrad bzw. zur Rückwand */}
      <rect className="demo-sensor" x="19" y="56" width="10" height="18" rx="2" />
      <line className="demo-beam" x1="30" y1="65" x2={box.bikePresent ? 44 : 140} y2="65" />
      {/* Fahrrad */}
      <g className={`demo-bike ${box.bikePresent ? 'is-present' : ''}`}>
        <circle cx="62" cy="84" r="14" />
        <circle cx="114" cy="84" r="14" />
        <path d="M62 84 L80 60 L104 60 L114 84 M80 60 L88 84 L104 60 M76 54 L86 54 M104 60 L100 50 L108 50" />
      </g>
      {/* LED */}
      <circle className={`demo-led ${box.ledGreen ? 'is-on' : ''}`} cx="36" cy="14" r="7" />
      {/* Servo-Riegel: dreht sich am Drehpunkt oben rechts nach oben, wenn er öffnet */}
      <circle className="demo-servo" cx="152" cy="28" r="6" />
      <rect className={`demo-latch ${box.lockOpen ? 'is-open' : ''}`} x="149" y="28" width="6" height="72" rx="3" />
    </svg>
  )
}
