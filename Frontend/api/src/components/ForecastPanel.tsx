import { useEffect, useState } from 'react'
import { api, type Forecast } from '../api'
import { hourLabel } from '../format'
import { useI18n } from '../i18n'

// Prognose wird selten neu berechnet – alle 5 Minuten reicht
const REFRESH_MS = 5 * 60 * 1000

export default function ForecastPanel() {
  const { t } = useI18n()
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
      <p className="section-kicker">{t('forecast.kicker')}</p>
      <h2 id="forecast-heading">{t('forecast.heading')}</h2>
      {!forecast || hours.length === 0 ? (
        <p className="chart-note">{t('forecast.none')}</p>
      ) : (
        <>
          <ul className="forecast-list">
            {hours.map((h) => (
              <li key={h.start} className="forecast-item">
                <span className="forecast-hour">{hourLabel(h.hour)}</span>
                <span className="forecast-bar" aria-hidden="true">
                  <span style={{ width: `${((h.expectedFree ?? 0) / forecast.totalSlots) * 100}%` }}></span>
                </span>
                <span className="forecast-value">
                  {t('forecast.free', { free: Math.round(h.expectedFree ?? 0), total: forecast.totalSlots })}
                </span>
              </li>
            ))}
          </ul>
          <p className="chart-note">{t('forecast.note', { days: forecast.basedOnDays })}</p>
        </>
      )}
    </section>
  )
}
