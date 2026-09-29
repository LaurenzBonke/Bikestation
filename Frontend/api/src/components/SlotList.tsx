import type { Slot } from '../api'
import { formatTime, pad2 } from '../format'
import { displayStatus, slotName, statusKey } from '../slotStatus'
import { useI18n } from '../i18n'

export default function SlotList({ slots, isAdmin }: { slots: Slot[]; isAdmin: boolean }) {
  const { t, locale } = useI18n()

  return (
    <>
      <div className="spots-heading">
        <div>
          <h3 className="section-kicker">{t('slots.heading')}</h3>
          <span className="spots-hint">{t('slots.hint')}</span>
        </div>
        <span className="total-spots">{t('slots.total', { count: pad2(slots.length) })}</span>
      </div>

      <ul className="spot-list">
        {slots.map((slot) => {
          const status = displayStatus(slot)
          const warning = isAdmin && (slot.possibleTampering || slot.hasAnomaly)
          const content = (
            <>
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
              <span className="spot-name">{slotName(t, slot.id)}</span>
              <SensorValues slot={slot} />
              <span className="sensor-values">{t('slot.boxState', { state: t(`state.${slot.boxState}`) })}</span>
            </span>
            <span className={`spot-status ${status}`}>
              {t(statusKey[status])}
              {status === 'free' && (
                <span className="spot-book">
                  {t('dash.bookNow')} <span aria-hidden="true">→</span>
                </span>
              )}
            </span>
            {isAdmin && slot.possibleTampering && (
              <p className="tamper-warning" role="alert">
                <span aria-hidden="true">⚠</span> {t('slot.tamper')}
              </p>
            )}
            {isAdmin && slot.hasAnomaly && (
              <p className="tamper-warning" role="alert">
                <span aria-hidden="true">⚠</span> {t('slot.anomaly')}
              </p>
            )}
            {status === 'offline' && (
              <p className="offline-note">{t('slot.offlineNote', { time: formatTime(slot.lastUpdated, locale) })}</p>
            )}
            </>
          )
          return (
            <li key={slot.id} className={warning ? 'has-warning' : undefined}>
              {/* Freie Plätze sind anklickbar und führen direkt zum Buchen */}
              {status === 'free' ? (
                <a className="spot-row is-bookable" href="#boxen" aria-label={t('slot.bookAria', { slot: slotName(t, slot.id) })}>
                  {content}
                </a>
              ) : (
                <div className="spot-row">{content}</div>
              )}
            </li>
          )
        })}
      </ul>
    </>
  )
}

function SensorValues({ slot }: { slot: Slot }) {
  const { t, locale } = useI18n()
  const reading = slot.latestReading
  if (!reading) {
    return <span className="sensor-values">{t('slot.noData')}</span>
  }

  return (
    <span className="sensor-values">
      {t('slot.values', {
        pressure: reading.pressure,
        distance: reading.distance,
        vibration: reading.vibration ? t('common.yes') : t('common.no'),
        time: formatTime(reading.timestamp, locale),
      })}
    </span>
  )
}
