import type { Alert } from '../api'
import { formatDateTime, formatNumber } from '../format'
import { alertMessage, slotName } from '../slotStatus'
import { useI18n } from '../i18n'

export default function AlertsPanel({ alerts }: { alerts: Alert[] }) {
  const { t, locale } = useI18n()

  return (
    <aside className="station-panel" aria-labelledby="alerts-heading">
      <div className="station-details">
        <p className="section-kicker">{t('alerts.kicker')}</p>
        <h2 id="alerts-heading">{t('alerts.heading')}</h2>

        {alerts.length === 0 ? (
          <p className="alerts-empty">
            <span className="ok-mark" aria-hidden="true">✓</span> {t('alerts.none')}
          </p>
        ) : (
          <ul className="alert-list">
            {alerts.map((alert) => (
              <li key={alert.id} className={`alert-item severity-${alert.severity.toLowerCase()}`}>
                <span className="alert-meta">
                  <span className="alert-badge">
                    <span aria-hidden="true">⚠</span> {t(`severity.${alert.severity}`)}
                  </span>
                  <span>{slotName(t, alert.slotId)}</span>
                  <span className="alert-type">
                    {t(`alertType.${alert.type}`)}
                    {alert.score !== null && ` · Score ${formatNumber(alert.score, locale)}`}
                  </span>
                  <time dateTime={alert.timestamp}>{formatDateTime(alert.timestamp, locale)}</time>
                </span>
                <span className="alert-message">{alertMessage(t, alert)}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="station-footer">
        <span className="footer-signal" aria-hidden="true"></span> {t('alerts.footer')}
      </div>
    </aside>
  )
}
