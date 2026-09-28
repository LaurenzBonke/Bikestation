import { useEffect, useState } from 'react'
import { api, type Forecast } from '../api'

// Prognose wird selten neu berechnet – alle 5 Minuten reicht
const REFRESH_MS = 5 * 60 * 1000

function hourLabel(hour: number) {
  return `${String(hour).padStart(2, '0')}:00`
}

function freeText(expectedFree: number, total: number) {
  const rounded = Math.round(expectedFree)
  return `ca. ${rounded} von ${total} frei`
}

export default function ForecastPanel() {
  const [forecast, setForecast] = useState<Forecast | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    const load = () =>
      api
        .getForecast(6, controller.signal)
        .then(setForecast)
        .catch(() => {
          // Prognose ist optional – bei Fehlern einfach nicht anzeigen
        })
    void load()
    const timer = window.setInterval(load, REFRESH_MS)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [])

  const hours = forecast?.hours.filter((h) => h.expectedFree !== null) ?? []

  return (
    <section className="forecast-panel" aria-labelledby="forecast-heading">
      <p className="section-kicker">PROGNOSE</p>
      <h2 id="forecast-heading">Die nächsten Stunden</h2>
      {!forecast || hours.length === 0 ? (
        <p className="chart-note">Noch zu wenig Daten für eine Prognose.</p>
      ) : (
        <>
          <ul className="forecast-list">
            {hours.map((h) => (
              <li key={h.start} className="forecast-item">
                <span className="forecast-hour">{hourLabel(h.hour)}</span>
                <span className="forecast-bar" aria-hidden="true">
                  <span style={{ width: `${((h.expectedFree ?? 0) / forecast.totalSlots) * 100}%` }}></span>
                </span>
                <span className="forecast-value">{freeText(h.expectedFree ?? 0, forecast.totalSlots)}</span>
              </li>
            ))}
          </ul>
          <p className="chart-note">Geschätzt aus der Belegung der letzten {forecast.basedOnDays} Tage.</p>
        </>
      )}
    </section>
  )
}
