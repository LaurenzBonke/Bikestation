import { useEffect, useState } from 'react'
import { api, type Statistics } from '../api'

const PERIODS = [
  { days: 1, label: '24 Stunden' },
  { days: 7, label: '7 Tage' },
  { days: 30, label: '30 Tage' },
]

function hourLabel(hour: number) {
  return `${String(hour).padStart(2, '0')}:00`
}

export default function StatisticsPage() {
  const [days, setDays] = useState(7)
  const [stats, setStats] = useState<Statistics | null>(null)
  const [error, setError] = useState('')
  const [showTable, setShowTable] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    api
      .getStatistics(days, controller.signal)
      .then(setStats)
      .catch(() => {
        if (!controller.signal.aborted) setError('Statistik konnte nicht geladen werden.')
      })
    return () => controller.abort()
  }, [days])

  return (
    <main id="main" className="page-shell" tabIndex={-1}>
      <section className="intro-row">
        <div>
          <p className="eyebrow">
            <span className="eyebrow-line" aria-hidden="true"></span> AUSWERTUNG
          </p>
          <h1>Statistik</h1>
          <p className="intro-copy">Auslastung und Ereignisse der Station.</p>
        </div>
        <div className="period-picker" role="group" aria-label="Zeitraum">
          {PERIODS.map((period) => (
            <button
              key={period.days}
              type="button"
              className={`period-button ${days === period.days ? 'active' : ''}`}
              aria-pressed={days === period.days}
              onClick={() => setDays(period.days)}
            >
              {period.label}
            </button>
          ))}
        </div>
      </section>

      {error && (
        <p className="error-notice" role="alert">
          {error}
        </p>
      )}

      {stats && (
        <>
          <section className="stat-tiles" aria-label="Kennzahlen">
            <Tile label="Durchschnittliche Auslastung" value={`${stats.occupancyPercent.toLocaleString('de-DE')} %`} />
            <Tile
              label="Stoßzeit"
              value={stats.busiestHour === null ? '–' : `${hourLabel(stats.busiestHour)} Uhr`}
            />
            <Tile label="Vibrations-Meldungen" value={String(stats.tamperEvents)} />
            <Tile label="KI-Anomalien" value={String(stats.anomalyEvents)} />
          </section>

          <section className="admin-panel admin-section" aria-labelledby="hourly-heading">
            <div className="chart-heading">
              <div>
                <p className="section-kicker">NACH UHRZEIT</p>
                <h2 id="hourly-heading">Auslastung nach Uhrzeit (%)</h2>
              </div>
              <button className="text-link-button" type="button" onClick={() => setShowTable((v) => !v)}>
                {showTable ? 'Als Diagramm anzeigen' : 'Als Tabelle anzeigen'}
              </button>
            </div>

            {stats.totalReadings === 0 ? (
              <p className="alerts-empty">Noch keine Messwerte in diesem Zeitraum.</p>
            ) : showTable ? (
              <HourTable stats={stats} />
            ) : (
              <HourChart stats={stats} />
            )}
            <p className="chart-note">
              Anteil der Messungen, bei denen ein Platz belegt war · {stats.totalReadings.toLocaleString('de-DE')}{' '}
              Messungen
            </p>
          </section>

          <section className="admin-panel admin-section" aria-labelledby="slots-heading">
            <p className="section-kicker">JE STELLPLATZ</p>
            <h2 id="slots-heading">Stellplätze im Vergleich</h2>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Stellplatz</th>
                    <th scope="col">Auslastung</th>
                    <th scope="col">Vibrations-Meldungen</th>
                    <th scope="col">KI-Anomalien</th>
                  </tr>
                </thead>
                <tbody>
                  {stats.slots.map((slot) => (
                    <tr key={slot.slotId}>
                      <th scope="row">{slot.name}</th>
                      <td>{slot.occupancyPercent.toLocaleString('de-DE')} %</td>
                      <td>{slot.tamperEvents}</td>
                      <td>{slot.anomalyEvents}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </main>
  )
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <div className="stat-tile">
      <span className="stat-label">{label}</span>
      <span className="stat-value">{value}</span>
    </div>
  )
}

// Balkendiagramm: eine Reihe, eine Farbe. Jeder Balken ist fokussierbar und zeigt einen Tooltip.
function HourChart({ stats }: { stats: Statistics }) {
  return (
    <div className="hour-chart">
      <div className="hour-grid" aria-hidden="true">
        {[100, 50, 0].map((line) => (
          <span key={line} className="grid-line" style={{ bottom: `${line}%` }}>
            <span>{line}</span>
          </span>
        ))}
      </div>
      <ol className="hour-bars">
        {stats.occupancyByHour.map((h) => (
          <li
            key={h.hour}
            className={`hour-bar ${h.hour === stats.busiestHour ? 'is-peak' : ''}`}
            tabIndex={0}
            aria-label={`${hourLabel(h.hour)} Uhr: ${h.occupancyPercent} Prozent belegt`}
          >
            <span className="bar-fill" style={{ height: `${Math.max(h.occupancyPercent, 0.5)}%` }}>
              <span className="bar-tooltip" role="tooltip">
                <strong>{hourLabel(h.hour)} Uhr</strong>
                {h.occupancyPercent.toLocaleString('de-DE')} % belegt
                <small>{h.readings} Messungen</small>
              </span>
            </span>
          </li>
        ))}
      </ol>
      <div className="hour-axis" aria-hidden="true">
        {[0, 6, 12, 18, 23].map((hour) => (
          <span key={hour} style={{ left: `${((hour + 0.5) / 24) * 100}%` }}>
            {String(hour).padStart(2, '0')}
          </span>
        ))}
      </div>
    </div>
  )
}

function HourTable({ stats }: { stats: Statistics }) {
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">Uhrzeit</th>
            <th scope="col">Auslastung</th>
            <th scope="col">Messungen</th>
          </tr>
        </thead>
        <tbody>
          {stats.occupancyByHour.map((h) => (
            <tr key={h.hour}>
              <th scope="row">{hourLabel(h.hour)} Uhr</th>
              <td>{h.occupancyPercent.toLocaleString('de-DE')} %</td>
              <td>{h.readings}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
