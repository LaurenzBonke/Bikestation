import type { StationData } from '../useStationData'
import { formatTime } from '../format'
import OccupancyFrame from './OccupancyFrame'
import SlotList from './SlotList'
import AlertsPanel from './AlertsPanel'
import ForecastPanel from './ForecastPanel'
import { displayStatus } from '../slotStatus'

type DashboardProps = {
  data: StationData
}

export default function Dashboard({ data }: DashboardProps) {
  const { slots, alerts, connected, loading, lastFetched } = data
  const free = slots.filter((slot) => displayStatus(slot) === 'free').length
  const occupied = slots.filter((slot) => displayStatus(slot) === 'taken').length
  const offline = slots.length - free - occupied

  return (
    <main id="main" className="page-shell" tabIndex={-1}>
      <section className="intro-row">
        <div>
          <p className="eyebrow">
            <span className="eyebrow-line" aria-hidden="true"></span> SMART BIKESTATION
          </p>
          <h1>Freien Stellplatz finden.</h1>
          <p className="intro-copy">Live-Belegung der Station – bevor du ankommst.</p>
        </div>
        <p className="last-update">
          Zuletzt aktualisiert: <strong>{formatTime(lastFetched)}</strong>
        </p>
      </section>

      {!connected && (
        <p className="error-notice" role="alert">
          Keine Verbindung zur API. Die angezeigten Daten sind eventuell veraltet. Läuft das Backend auf Port 5137?
        </p>
      )}

      <section className="dashboard-grid" aria-label="Belegung der Station">
        <div className="availability-panel">
          <div className="panel-heading">
            <div>
              <p className="section-kicker">JETZT</p>
              <h2>Verfügbare Stellplätze</h2>
            </div>
            <span className="live-label">
              <span aria-hidden="true"></span> Aktualisierung alle 3 s
            </span>
          </div>

          {loading ? (
            <p className="loading-text">Daten werden geladen …</p>
          ) : (
            <>
              <div className="availability-summary">
                <div className="count-block" aria-live="polite">
                  <span className="available-count">{free}</span>
                  <span className="count-total">
                    von {slots.length}
                    <br />
                    Plätzen frei
                  </span>
                </div>
                <OccupancyFrame slots={slots} />
              </div>
              <p className="summary-line">
                {free} frei · {occupied} belegt
                {offline > 0 && ` · ${offline} offline / ohne Daten`}
              </p>

              <div className="divider"></div>
              <SlotList slots={slots} />
            </>
          )}
        </div>

        <div className="side-column">
          <AlertsPanel alerts={alerts} slots={slots} />
          <ForecastPanel />
        </div>
      </section>

      <footer className="page-footer">
        <span>Smart Bikestation – Schulprojekt-Prototyp</span>
        <span>
          Keine Kameras, keine personenbezogenen Daten <span className="footer-star" aria-hidden="true">✳</span>
        </span>
      </footer>
    </main>
  )
}
