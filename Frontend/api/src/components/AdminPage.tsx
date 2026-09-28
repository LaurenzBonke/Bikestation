import { useState, type FormEvent } from 'react'
import { ApiError, api, type Alert } from '../api'
import type { Auth } from '../useAuth'
import type { StationData } from '../useStationData'
import { formatDateTime, formatTime } from '../format'

type AdminPageProps = {
  auth: Auth
  data: StationData
}

export default function AdminPage({ auth, data }: AdminPageProps) {
  return (
    <main id="main" className={`admin-shell ${auth.session ? '' : 'login-shell'}`} tabIndex={-1}>
      {auth.session ? <AdminArea auth={auth} data={data} /> : <LoginForm auth={auth} />}
    </main>
  )
}

function LoginForm({ auth }: { auth: Auth }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    try {
      await auth.login(username.trim(), password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Anmeldung fehlgeschlagen.')
      setSubmitting(false)
    }
  }

  return (
    <>
      <section className="admin-heading">
        <p className="eyebrow">
          <span className="eyebrow-line" aria-hidden="true"></span> GESCHÜTZTER BEREICH
        </p>
        <h1>Admin-Anmeldung</h1>
        <p>Melde dich an, um Meldungen der Station zu bearbeiten.</p>
      </section>

      <form className="admin-panel login-panel" onSubmit={handleSubmit}>
        {auth.logoutReason && !error && (
          <p className="form-error" role="status">
            {auth.logoutReason}
          </p>
        )}
        <label className="form-field">
          <span>Benutzername</span>
          <input
            name="username"
            autoComplete="username"
            required
            maxLength={64}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
        </label>
        <label className="form-field">
          <span>Passwort</span>
          <input
            name="password"
            type="password"
            autoComplete="current-password"
            required
            maxLength={256}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? 'Anmelden …' : 'Anmelden'} <span aria-hidden="true">→</span>
        </button>
      </form>
    </>
  )
}

function AdminArea({ auth, data }: AdminPageProps) {
  const session = auth.session!
  const [message, setMessage] = useState('')
  const [pendingId, setPendingId] = useState<number | null>(null)

  const slotName = (id: number) => data.slots.find((slot) => slot.id === id)?.name ?? `Stellplatz ${id}`

  async function resolve(alert: Alert) {
    setPendingId(alert.id)
    setMessage('')
    try {
      await api.resolveAlert(alert.id, session.token)
      setMessage(`Meldung an ${slotName(alert.slotId)} wurde als erledigt markiert.`)
      data.refresh()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        auth.logout('Deine Sitzung ist abgelaufen. Bitte erneut anmelden.')
        return
      }
      setMessage(err instanceof Error ? err.message : 'Aktion fehlgeschlagen.')
    } finally {
      setPendingId(null)
    }
  }

  return (
    <>
      <section className="admin-heading">
        <p className="eyebrow">
          <span className="eyebrow-line" aria-hidden="true"></span> STATIONSVERWALTUNG
        </p>
        <h1>Hallo, {session.username}.</h1>
        <p>Offene Meldungen prüfen und nach Kontrolle vor Ort als erledigt markieren.</p>
      </section>

      <section className="admin-toolbar">
        <p className="last-update">
          Angemeldet bis <strong>{formatTime(session.expiresAt)}</strong>
        </p>
        <div className="admin-account">
          <span className="account-avatar" aria-hidden="true">
            {session.username[0]?.toUpperCase()}
          </span>
          <span>{session.username}</span>
          <button className="text-button" type="button" onClick={() => auth.logout()}>
            Abmelden
          </button>
        </div>
      </section>

      <section className="admin-panel admin-section" aria-labelledby="admin-alerts-heading">
        <p className="section-kicker">MELDUNGEN</p>
        <h2 id="admin-alerts-heading">Offene Meldungen ({data.alerts.length})</h2>

        <p className="save-message" role="status">
          {message}
        </p>

        {data.alerts.length === 0 ? (
          <p className="alerts-empty">
            <span className="ok-mark" aria-hidden="true">✓</span> Keine offenen Meldungen.
          </p>
        ) : (
          <ul className="alert-list">
            {data.alerts.map((alert) => (
              <li key={alert.id} className={`alert-item severity-${alert.severity.toLowerCase()}`}>
                <span className="alert-meta">
                  <span className="alert-badge">
                    <span aria-hidden="true">⚠</span> {slotName(alert.slotId)}
                  </span>
                  <time dateTime={alert.timestamp}>{formatDateTime(alert.timestamp)}</time>
                </span>
                <span className="alert-message">{alert.message}</span>
                <span className="alert-actions">
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={pendingId === alert.id}
                    onClick={() => resolve(alert)}
                  >
                    {pendingId === alert.id ? 'Wird gespeichert …' : 'Als erledigt markieren'}
                    <span className="visually-hidden"> – {slotName(alert.slotId)}, {formatDateTime(alert.timestamp)}</span>
                  </button>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  )
}
