import type { Alert, Slot } from '../api'
import { formatDateTime } from '../format'

type AlertsPanelProps = {
  alerts: Alert[]
  slots: Slot[]
}

const severityText: Record<Alert['severity'], string> = {
  Info: 'Hinweis',
  Warning: 'Warnung',
  Critical: 'Kritisch',
}

export default function AlertsPanel({ alerts, slots }: AlertsPanelProps) {
  const slotName = (id: number) => slots.find((slot) => slot.id === id)?.name ?? `Stellplatz ${id}`

  return (
    <aside className="station-panel" aria-labelledby="alerts-heading">
      <div className="station-details">
        <p className="section-kicker">MELDUNGEN</p>
        <h2 id="alerts-heading">Offene Meldungen</h2>

        {alerts.length === 0 ? (
          <p className="alerts-empty">
            <span className="ok-mark" aria-hidden="true">✓</span> Keine offenen Meldungen. Alles in Ordnung.
          </p>
        ) : (
          <ul className="alert-list">
            {alerts.map((alert) => (
              <li key={alert.id} className={`alert-item severity-${alert.severity.toLowerCase()}`}>
                <span className="alert-meta">
                  <span className="alert-badge">
                    <span aria-hidden="true">⚠</span> {severityText[alert.severity]}
                  </span>
                  <span>{slotName(alert.slotId)}</span>
                  <time dateTime={alert.timestamp}>{formatDateTime(alert.timestamp)}</time>
                </span>
                <span className="alert-message">{alert.message}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="station-footer">
        <span className="footer-signal" aria-hidden="true"></span> Vibration an einem Stellplatz erzeugt eine Meldung
      </div>
    </aside>
  )
}
