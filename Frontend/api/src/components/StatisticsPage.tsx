import { useEffect, useState } from 'react'
import { api, type DailyOccupancy, type Statistics } from '../api'
import { formatDay, formatNumber, hourLabel } from '../format'
import { slotName } from '../slotStatus'
import { useI18n, type TranslationKey } from '../i18n'

const PERIODS: { days: number; label: TranslationKey }[] = [
  { days: 1, label: 'stats.period1' },
  { days: 7, label: 'stats.period7' },
  { days: 30, label: 'stats.period30' },
]

export default function StatisticsPage() {
  const { t, locale } = useI18n()
  const [days, setDays] = useState(7)
  const [stats, setStats] = useState<Statistics | null>(null)
  const [error, setError] = useState(false)
  const [showTable, setShowTable] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    setError(false)
    api
      .getStatistics(days, controller.signal)
      .then(setStats)
      .catch(() => {
        if (!controller.signal.aborted) setError(true)
      })
    return () => controller.abort()
  }, [days])

  return (
    <main id="main" className="page-shell" tabIndex={-1}>
      <section className="intro-row">
        <div>
          <p className="eyebrow">
            <span className="eyebrow-line" aria-hidden="true"></span> {t('stats.eyebrow')}
          </p>
          <h1>{t('stats.title')}</h1>
          <p className="intro-copy">{t('stats.intro')}</p>
        </div>
        <div className="period-picker" role="group" aria-label={t('stats.period')}>
          {PERIODS.map((period) => (
            <button
              key={period.days}
              type="button"
              className={`period-button ${days === period.days ? 'active' : ''}`}
              aria-pressed={days === period.days}
              onClick={() => setDays(period.days)}
            >
              {t(period.label)}
            </button>
          ))}
        </div>
      </section>

      {error && (
        <p className="error-notice" role="alert">
          {t('stats.error')}
        </p>
      )}

      {stats && (
        <>
          <section className="stat-tiles" aria-label={t('stats.tiles')}>
            <Tile label={t('stats.avg')} value={`${formatNumber(stats.occupancyPercent, locale)} %`} />
            <Tile
              label={t('stats.peak')}
              value={stats.busiestHour === null ? '–' : t('stats.peakValue', { hour: hourLabel(stats.busiestHour) })}
            />
            <Tile label={t('stats.tamper')} value={String(stats.tamperEvents)} />
            <Tile label={t('stats.anomalies')} value={String(stats.anomalyEvents)} />
          </section>

          <section className="admin-panel admin-section" aria-labelledby="hourly-heading">
            <div className="chart-heading">
              <div>
                <p className="section-kicker">{t('stats.byHourKicker')}</p>
                <h2 id="hourly-heading">{t('stats.byHour')}</h2>
              </div>
              <button className="text-link-button" type="button" onClick={() => setShowTable((v) => !v)}>
                {showTable ? t('stats.showChart') : t('stats.showTable')}
              </button>
            </div>

            {stats.totalReadings === 0 ? (
              <p className="alerts-empty">{t('stats.empty')}</p>
            ) : showTable ? (
              <HourTable stats={stats} />
            ) : (
              <HourChart stats={stats} />
            )}
            <p className="chart-note">{t('stats.note', { count: formatNumber(stats.totalReadings, locale) })}</p>
          </section>

          <section className="admin-panel admin-section" aria-labelledby="daily-heading">
            <p className="section-kicker">{t('stats.byDayKicker')}</p>
            <h2 id="daily-heading">{t('stats.byDay')}</h2>
            {stats.occupancyByDay.length === 0 ? (
              <p className="alerts-empty">{t('stats.empty')}</p>
            ) : showTable ? (
              <DayTable days={stats.occupancyByDay} />
            ) : (
              <DayChart days={stats.occupancyByDay} />
            )}
          </section>

          <section className="admin-panel admin-section" aria-labelledby="slots-heading">
            <p className="section-kicker">{t('stats.bySlotKicker')}</p>
            <h2 id="slots-heading">{t('stats.bySlot')}</h2>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">{t('stats.colSlot')}</th>
                    <th scope="col">{t('stats.colOccupancy')}</th>
                    <th scope="col">{t('stats.tamper')}</th>
                    <th scope="col">{t('stats.anomalies')}</th>
                  </tr>
                </thead>
                <tbody>
                  {stats.slots.map((slot) => (
                    <tr key={slot.slotId}>
                      <th scope="row">{slotName(t, slot.slotId)}</th>
                      <td>{formatNumber(slot.occupancyPercent, locale)} %</td>
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

function ChartGrid() {
  return (
    <div className="hour-grid" aria-hidden="true">
      {[100, 50, 0].map((line) => (
        <span key={line} className="grid-line" style={{ bottom: `${line}%` }}>
          <span>{line}</span>
        </span>
      ))}
    </div>
  )
}

// Balkendiagramm: eine Reihe, eine Farbe. Jeder Balken ist fokussierbar und zeigt einen Tooltip.
function HourChart({ stats }: { stats: Statistics }) {
  const { t, locale } = useI18n()
  return (
    <div className="hour-chart">
      <ChartGrid />
      <ol className="hour-bars">
        {stats.occupancyByHour.map((h) => (
          <li
            key={h.hour}
            className={`hour-bar ${h.hour === stats.busiestHour ? 'is-peak' : ''}`}
            tabIndex={0}
            aria-label={t('stats.hourAria', { hour: hourLabel(h.hour), percent: formatNumber(h.occupancyPercent, locale) })}
          >
            <span className="bar-fill" style={{ height: `${Math.max(h.occupancyPercent, 0.5)}%` }}>
              <span className="bar-tooltip" role="tooltip">
                <strong>{t('stats.tooltipHour', { hour: hourLabel(h.hour) })}</strong>
                {t('stats.tooltipOccupied', { percent: formatNumber(h.occupancyPercent, locale) })}
                <small>{t('stats.tooltipReadings', { count: formatNumber(h.readings, locale) })}</small>
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
  const { t, locale } = useI18n()
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">{t('stats.colTime')}</th>
            <th scope="col">{t('stats.colOccupancy')}</th>
            <th scope="col">{t('stats.colReadings')}</th>
          </tr>
        </thead>
        <tbody>
          {stats.occupancyByHour.map((h) => (
            <tr key={h.hour}>
              <th scope="row">{t('stats.tooltipHour', { hour: hourLabel(h.hour) })}</th>
              <td>{formatNumber(h.occupancyPercent, locale)} %</td>
              <td>{formatNumber(h.readings, locale)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function weekday(date: string, locale: string) {
  return new Intl.DateTimeFormat(locale, { weekday: 'short' }).format(new Date(`${date}T12:00:00`))
}

// Gleicher Aufbau wie das Stunden-Diagramm: eine Reihe, eine Farbe, Tooltip pro Balken
function DayChart({ days }: { days: DailyOccupancy[] }) {
  const { t, locale } = useI18n()
  const labelEvery = Math.ceil(days.length / 8)
  return (
    <div className="hour-chart">
      <ChartGrid />
      <ol className="hour-bars" style={{ gridTemplateColumns: `repeat(${days.length}, 1fr)` }}>
        {days.map((d) => (
          <li
            key={d.date}
            className="hour-bar"
            tabIndex={0}
            aria-label={t('stats.dayAria', {
              day: formatDay(d.date, locale),
              percent: formatNumber(d.occupancyPercent, locale),
              tamper: d.tamperEvents,
              anomalies: d.anomalyEvents,
            })}
          >
            <span className="bar-fill" style={{ height: `${Math.max(d.occupancyPercent, 0.5)}%` }}>
              <span className="bar-tooltip" role="tooltip">
                <strong>{formatDay(d.date, locale)}</strong>
                {t('stats.tooltipOccupied', { percent: formatNumber(d.occupancyPercent, locale) })}
                <small>{t('stats.tooltipEvents', { tamper: d.tamperEvents, anomalies: d.anomalyEvents })}</small>
              </span>
            </span>
          </li>
        ))}
      </ol>
      <div className="hour-axis" aria-hidden="true">
        {days.map((d, i) =>
          i % labelEvery === 0 ? (
            <span key={d.date} style={{ left: `${((i + 0.5) / days.length) * 100}%` }}>
              {weekday(d.date, locale)}
            </span>
          ) : null,
        )}
      </div>
    </div>
  )
}

function DayTable({ days }: { days: DailyOccupancy[] }) {
  const { t, locale } = useI18n()
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">{t('stats.colDay')}</th>
            <th scope="col">{t('stats.colOccupancy')}</th>
            <th scope="col">{t('stats.tamper')}</th>
            <th scope="col">{t('stats.anomalies')}</th>
          </tr>
        </thead>
        <tbody>
          {days.map((d) => (
            <tr key={d.date}>
              <th scope="row">{formatDay(d.date, locale)}</th>
              <td>{formatNumber(d.occupancyPercent, locale)} %</td>
              <td>{d.tamperEvents}</td>
              <td>{d.anomalyEvents}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
