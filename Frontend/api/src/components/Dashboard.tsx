import type { StationData } from '../useStationData'
import { formatTime } from '../format'
import OccupancyFrame from './OccupancyFrame'
import SlotList from './SlotList'
import AlertsPanel from './AlertsPanel'
import ForecastPanel from './ForecastPanel'
import { displayStatus } from '../slotStatus'
import { useI18n } from '../i18n'

type DashboardProps = {
  data: StationData
}

export default function Dashboard({ data }: DashboardProps) {
  const { t, locale } = useI18n()
  const { slots, alerts, connected, loading, lastFetched } = data
  const free = slots.filter((slot) => displayStatus(slot) === 'free').length
  const occupied = slots.filter((slot) => displayStatus(slot) === 'taken').length
  const offline = slots.length - free - occupied

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
              <h2>{t('dash.available')}</h2>
            </div>
            <span className="live-label">
              <span aria-hidden="true"></span> {t('dash.refresh')}
            </span>
          </div>

          {loading ? (
            <p className="loading-text">{t('dash.loading')}</p>
          ) : (
            <>
              <div className="availability-summary">
                <div className="count-block" aria-live="polite">
                  <span className="available-count">{free}</span>
                  <span className="count-total">
                    {t('dash.of', { total: slots.length })}
                    <br />
                    {t('dash.spotsFree')}
                  </span>
                </div>
                <OccupancyFrame slots={slots} />
              </div>
              <p className="summary-line">
                {t('dash.summary', { free, taken: occupied })}
                {offline > 0 && t('dash.summaryOffline', { offline })}
              </p>

              <div className="divider"></div>
              <SlotList slots={slots} />
            </>
          )}
        </div>

        <div className="side-column">
          <AlertsPanel alerts={alerts} />
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
