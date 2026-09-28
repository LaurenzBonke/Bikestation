import { useState, type FormEvent } from 'react'
import { ApiError, api, type Alert } from '../api'
import type { Auth } from '../useAuth'
import type { StationData } from '../useStationData'
import { formatDateTime, formatNumber, formatTime } from '../format'
import { alertMessage, slotName } from '../slotStatus'
import { useI18n, type TranslationKey } from '../i18n'

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
  const { t } = useI18n()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<TranslationKey | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await auth.login(username.trim(), password)
    } catch (err) {
      const status = err instanceof ApiError ? err.status : 0
      setError(status === 401 ? 'login.invalid' : status === 429 ? 'error.tooMany' : 'error.generic')
      setSubmitting(false)
    }
  }

  return (
    <>
      <section className="admin-heading">
        <p className="eyebrow">
          <span className="eyebrow-line" aria-hidden="true"></span> {t('login.eyebrow')}
        </p>
        <h1>{t('login.title')}</h1>
        <p>{t('login.intro')}</p>
      </section>

      <form className="admin-panel login-panel" onSubmit={handleSubmit}>
        {auth.logoutReason && !error && (
          <p className="form-error" role="status">
            {t(auth.logoutReason)}
          </p>
        )}
        <label className="form-field">
          <span>{t('login.username')}</span>
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
          <span>{t('login.password')}</span>
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
            {t(error)}
          </p>
        )}
        <button className="primary-button" type="submit" disabled={submitting}>
          {submitting ? t('login.submitting') : t('login.submit')} <span aria-hidden="true">→</span>
        </button>
      </form>
    </>
  )
}

function AdminArea({ auth, data }: AdminPageProps) {
  const { t, locale } = useI18n()
  const session = auth.session!
  const [message, setMessage] = useState('')
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [generating, setGenerating] = useState(false)

  async function resolve(alert: Alert) {
    setPendingId(alert.id)
    setMessage('')
    try {
      await api.resolveAlert(alert.id, session.token)
      setMessage(t('admin.resolved', { slot: slotName(t, alert.slotId) }))
      data.refresh()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        auth.logout('login.expired')
        return
      }
      setMessage(t('error.generic'))
    } finally {
      setPendingId(null)
    }
  }

  async function generateDemoData() {
    setGenerating(true)
    setMessage('')
    try {
      const result = await api.generateDemoData(7, session.token)
      setMessage(t('admin.demoDone', { count: formatNumber(result.readings, locale) }))
      data.refresh()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        auth.logout('login.expired')
        return
      }
      setMessage(err instanceof ApiError && err.status === 404 ? t('admin.demoDisabled') : t('error.generic'))
    } finally {
      setGenerating(false)
    }
  }

  return (
    <>
      <section className="admin-heading">
        <p className="eyebrow">
          <span className="eyebrow-line" aria-hidden="true"></span> {t('admin.eyebrow')}
        </p>
        <h1>{t('admin.hello', { name: session.username })}</h1>
        <p>{t('admin.intro')}</p>
      </section>

      <section className="admin-toolbar">
        <p className="last-update">
          {t('admin.loggedInUntil')} <strong>{formatTime(session.expiresAt, locale)}</strong>
        </p>
        <div className="admin-account">
          <span className="account-avatar" aria-hidden="true">
            {session.username[0]?.toUpperCase()}
          </span>
          <span>{session.username}</span>
          <button className="text-button" type="button" onClick={() => auth.logout()}>
            {t('admin.logout')}
          </button>
        </div>
      </section>

      <section className="admin-panel admin-section" aria-labelledby="admin-alerts-heading">
        <p className="section-kicker">{t('alerts.kicker')}</p>
        <h2 id="admin-alerts-heading">{t('admin.alertsHeading', { count: data.alerts.length })}</h2>

        <p className="save-message" role="status">
          {message}
        </p>

        {data.alerts.length === 0 ? (
          <p className="alerts-empty">
            <span className="ok-mark" aria-hidden="true">✓</span> {t('alerts.noneShort')}
          </p>
        ) : (
          <ul className="alert-list">
            {data.alerts.map((alert) => (
              <li key={alert.id} className={`alert-item severity-${alert.severity.toLowerCase()}`}>
                <span className="alert-meta">
                  <span className="alert-badge">
                    <span aria-hidden="true">⚠</span> {slotName(t, alert.slotId)}
                  </span>
                  <span className="alert-type">{t(`alertType.${alert.type}`)}</span>
                  <time dateTime={alert.timestamp}>{formatDateTime(alert.timestamp, locale)}</time>
                </span>
                <span className="alert-message">{alertMessage(t, alert)}</span>
                <span className="alert-actions">
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={pendingId === alert.id}
                    onClick={() => resolve(alert)}
                  >
                    {pendingId === alert.id ? t('admin.resolving') : t('admin.resolve')}
                    <span className="visually-hidden">
                      {' '}
                      – {slotName(t, alert.slotId)}, {formatDateTime(alert.timestamp, locale)}
                    </span>
                  </button>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="admin-panel admin-section" aria-labelledby="demo-heading">
        <p className="section-kicker">{t('admin.demoKicker')}</p>
        <h2 id="demo-heading">{t('admin.demoHeading')}</h2>
        <p className="chart-note">{t('admin.demoText')}</p>
        <div className="admin-actions">
          <button className="secondary-button" type="button" disabled={generating} onClick={generateDemoData}>
            {generating ? t('admin.demoGenerating') : t('admin.demoButton')}
          </button>
        </div>
      </section>
    </>
  )
}
