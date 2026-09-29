import type { StationData } from '../useStationData'
import { formatTime } from '../format'
import SlotList from './SlotList'
import AlertsPanel from './AlertsPanel'
import ForecastPanel from './ForecastPanel'
import { displayStatus, slotName } from '../slotStatus'
import { bookLink } from '../useRoute'
import { useI18n } from '../i18n'

type DashboardProps = {
  data: StationData
  isAdmin: boolean
}

export default function Dashboard({ data, isAdmin }: DashboardProps) {
  const { t, locale } = useI18n()
  const { slots, alerts, connected, loading, lastFetched } = data
  const free = slots.filter((slot) => displayStatus(slot) === 'free').length
  const occupied = slots.filter((slot) => displayStatus(slot) === 'taken').length
  const offline = slots.length - free - occupied
  const freeSlots = slots.filter((slot) => displayStatus(slot) === 'free')

  return (
    <main id="main" className="page-shell" tabIndex={-1}>
      <section className="intro-row">
        <div>
          <p className="eyebrow">
            <span className="eyebrow-line" aria-hidden="true"></span> {t('dash.eyebrow')}
          </p>
          <h1>{t('dash.title')}</h1>
          <p className="intro-copy">{t('dash.intro')}</p>
        </div>
        <p className="last-update">
          {t('dash.lastUpdate')} <strong>{formatTime(lastFetched, locale)}</strong>
        </p>
      </section>

      {!connected && (
        <p className="error-notice" role="alert">
          {t('dash.noConnection')}
        </p>
      )}

      <section className="dashboard-grid" aria-label={t('dash.stationAria')}>
        <div className="availability-panel">
          <div className="panel-heading">
            <div>
              <p className="section-kicker">{t('dash.now')}</p>
              <h2>{t('dash.freeHeading')}</h2>
            </div>
            <span className="live-label">
              <span aria-hidden="true"></span> {t('dash.refresh')}
            </span>
          </div>

          {loading ? (
            <p className="loading-text">{t('dash.loading')}</p>
          ) : (
            <>
              <div className="availability-answer" aria-live="polite">
                {free > 0 ? (
                  <ul className="free-spots">
                    {freeSlots.map((slot) => (
                      <li key={slot.id}>
                        <a className="free-callout" href={bookLink(slot.id)}>
                          <span className="free-callout-body">
                            <span className="free-callout-text">{t('dash.spotFree', { slot: slotName(t, slot.id) })}</span>
                            {slot.location && (
                              <span className="free-callout-location">
                                <PinIcon /> {slot.location}
                              </span>
                            )}
                          </span>
                          <span className="free-callout-action">
                            {t('dash.bookNow')} <span aria-hidden="true">→</span>
                          </span>
                        </a>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="none-free">{offline === slots.length ? t('dash.stationOffline') : t('dash.noneFree')}</p>
                )}
              </div>
              <p className="summary-line">
                {t('dash.summary', { free, taken: occupied })}
                {offline > 0 && t('dash.summaryOffline', { offline })}
              </p>

              <div className="divider"></div>
              <SlotList slots={slots} isAdmin={isAdmin} />
            </>
          )}
        </div>

        <div className="side-column">
          {isAdmin && <AlertsPanel alerts={alerts} />}
          <ForecastPanel />
        </div>
      </section>

      <footer className="page-footer">
        <span>{t('dash.footer')}</span>
        <span>
          {t('dash.privacy')} <span className="footer-star" aria-hidden="true">✳</span>
        </span>
      </footer>
    </main>
  )
}

export function PinIcon() {
  return (
    <svg className="pin-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none">
      <path d="M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21Z" />
      <circle cx="12" cy="9.5" r="2.5" />
    </svg>
  )
}
