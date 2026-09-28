import { useState } from 'react'
import { api, type Alert } from '../api'
import type { Auth } from '../useAuth'
import type { MyStatus } from '../useMyStatus'
import { formatDateTime } from '../format'
import { slotName } from '../slotStatus'
import { useI18n } from '../i18n'

// Meldungen, die den angemeldeten Nutzer betreffen – auf jeder Seite sichtbar, bis er sie bestätigt
export default function AlarmBanner({ auth, status }: { auth: Auth; status: MyStatus }) {
  const { t, locale } = useI18n()
  const [pending, setPending] = useState<number | null>(null)
  const alerts = status.me?.alerts ?? []
  if (!auth.session || alerts.length === 0) return null

  async function acknowledge(alert: Alert) {
    setPending(alert.id)
    try {
      await api.ackAlert(alert.id, auth.session!.token)
      status.refresh()
    } finally {
      setPending(null)
    }
  }

  return (
    <div className="alarm-stack" role="alert">
      {alerts.map((alert) => {
        const slot = slotName(t, alert.slotId)
        const critical = alert.type === 'BikeRemoved'
        return (
          <section key={alert.id} className={`alarm-banner ${critical ? 'is-critical' : ''}`}>
            <span className="alarm-icon" aria-hidden="true">
              ⚠
            </span>
            <div className="alarm-text">
              <strong>
                {alert.type === 'BikeRemoved'
                  ? t('alarm.bikeRemoved', { slot })
                  : alert.type === 'Anomaly'
                    ? t('alarm.anomaly', { slot })
                    : t('alarm.tamper', { slot })}
              </strong>
              {critical && <span>{t('alarm.bikeRemovedHint')}</span>}
              <time dateTime={alert.timestamp}>{formatDateTime(alert.timestamp, locale)}</time>
            </div>
            <button
              className="secondary-button"
              type="button"
              disabled={pending === alert.id}
              onClick={() => acknowledge(alert)}
            >
              {t('alarm.ack')}
            </button>
          </section>
        )
      })}
    </div>
  )
}
