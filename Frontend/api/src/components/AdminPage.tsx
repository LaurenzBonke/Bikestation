import { useEffect, useState, type FormEvent } from 'react'
import { ApiError, api, type Alert, type BoxEvent, type BoxOccupancy, type DemoInfo } from '../api'
import { DemoAccounts } from './AccountPage'
import { PinIcon } from './Dashboard'
import type { Auth } from '../useAuth'
import type { StationData } from '../useStationData'
import { formatDateTime, formatNumber, formatTime } from '../format'
import { alertMessage, slotName } from '../slotStatus'
import { useBoxes } from '../useBoxes'
import { useI18n, type TranslationKey } from '../i18n'

type AdminPageProps = {
  auth: Auth
  data: StationData
  demoAccounts: DemoInfo['accounts']
}

export default function AdminPage({ auth, data, demoAccounts }: AdminPageProps) {
  const { t } = useI18n()
  return (
    <main id="main" className={`admin-shell ${auth.session ? '' : 'login-shell'}`} tabIndex={-1}>
      {!auth.session && <LoginForm auth={auth} />}
      {!auth.session && <DemoAccounts auth={auth} accounts={demoAccounts.filter((a) => a.role === 'Admin')} />}
      {auth.session && auth.session.role !== 'Admin' && (
        <p className="error-notice" role="alert">
          {t('admin.noPermission')}
        </p>
      )}
      {auth.session?.role === 'Admin' && <AdminArea auth={auth} data={data} />}
    </main>
  )
}

// Nach einem Alarm gesperrte Boxen: Admin prüft vor Ort und gibt sie hier wieder frei
function BlockedBoxes({ auth }: { auth: Auth }) {
  const { t } = useI18n()
  const token = auth.session!.token
  const { boxes, refresh } = useBoxes(token)
  const [message, setMessage] = useState('')
  const blocked = boxes.filter((b) => b.state === 'Blocked')

  async function release(id: number) {
    try {
      await api.releaseBox(id, token)
      setMessage(t('admin.released', { slot: slotName(t, id) }))
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) auth.logout('login.expired')
      else setMessage(t('error.generic'))
    } finally {
      refresh()
    }
  }

  return (
    <section className="admin-panel admin-section" aria-labelledby="blocked-heading">
      <p className="section-kicker">{t('admin.blockedKicker')}</p>
      <h2 id="blocked-heading">{t('admin.blockedHeading')}</h2>
      <p className="chart-note">{t('admin.blockedText')}</p>
      <p className="save-message" role="status">
        {message}
      </p>
      {blocked.length === 0 ? (
        <p className="alerts-empty">
          <span className="ok-mark" aria-hidden="true">✓</span> {t('admin.noBlocked')}
        </p>
      ) : (
        <ul className="alert-list">
          {blocked.map((box) => (
            <li key={box.id} className="alert-item">
              <span className="alert-message">
                {slotName(t, box.id)} – {t('state.Blocked')}
              </span>
              <span className="alert-actions">
                <button className="secondary-button" type="button" onClick={() => release(box.id)}>
                  {t('admin.release')}
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

// Stationen hinzufügen, Ort ändern, löschen
function Stations({ auth }: { auth: Auth }) {
  const { t } = useI18n()
  const token = auth.session!.token
  const { boxes, refresh } = useBoxes(token)
  const [newLocation, setNewLocation] = useState('')
  const [editing, setEditing] = useState<{ id: number; location: string } | null>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true)
    setMessage('')
    try {
      await action()
      setMessage(success)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) auth.logout('login.expired')
      else if (err instanceof ApiError && err.status === 409) setMessage(t('stations.inUse'))
      else setMessage(t('error.generic'))
    } finally {
      setBusy(false)
      refresh()
    }
  }

  async function add(event: FormEvent) {
    event.preventDefault()
    const location = newLocation.trim()
    if (!location) return
    await run(async () => {
      const created = await api.addStation(location, token)
      setNewLocation('')
      return created
    }, t('stations.added', { location }))
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (!editing || !editing.location.trim()) return
    const { id, location } = editing
    await run(() => api.updateStation(id, location.trim(), token), t('stations.saved', { slot: slotName(t, id) }))
    setEditing(null)
  }

  async function remove(id: number, location: string) {
    const label = location ? `${slotName(t, id)} (${location})` : slotName(t, id)
    if (!window.confirm(t('stations.confirmDelete', { slot: label }))) return
    await run(() => api.deleteStation(id, token), t('stations.deleted', { slot: label }))
  }

  return (
    <section className="admin-panel admin-section" aria-labelledby="stations-heading">
      <p className="section-kicker">{t('stations.kicker')}</p>
      <h2 id="stations-heading">{t('stations.heading')}</h2>
      <p className="chart-note">{t('stations.text')}</p>
      <p className="save-message" role="status">
        {message}
      </p>
      <ul className="station-list">
        {boxes.map((box) => (
          <li key={box.id} className="station-item">
            {editing?.id === box.id ? (
              <form className="station-edit" onSubmit={save}>
                <label className="form-field">
                  <span>{t('stations.locationFor', { slot: slotName(t, box.id) })}</span>
                  <input
                    value={editing.location}
                    maxLength={80}
                    required
                    autoFocus
                    onChange={(event) => setEditing({ id: box.id, location: event.target.value })}
                  />
                </label>
                <button className="primary-button" type="submit" disabled={busy}>
                  {t('stations.save')}
                </button>
                <button className="secondary-button" type="button" onClick={() => setEditing(null)}>
                  {t('stations.cancel')}
                </button>
              </form>
            ) : (
              <>
                <div className="occupancy-box">
                  <strong>{slotName(t, box.id)}</strong>
                  <span className="spot-location">
                    <PinIcon /> {box.location || t('stations.noLocation')}
                  </span>
                </div>
                <span className="occupancy-since">{t('stations.deviceId', { id: box.id })}</span>
                <span className="station-actions">
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={busy}
                    onClick={() => setEditing({ id: box.id, location: box.location })}
                  >
                    {t('stations.edit')}
                  </button>
                  <button
                    className="secondary-button danger-button"
                    type="button"
                    disabled={busy || box.state !== 'Free'}
                    title={box.state !== 'Free' ? t('stations.inUse') : undefined}
                    onClick={() => remove(box.id, box.location)}
                  >
                    {t('stations.delete')}
                  </button>
                </span>
              </>
            )}
          </li>
        ))}
      </ul>
      <form className="station-add" onSubmit={add}>
        <label className="form-field">
          <span>{t('stations.newLocation')}</span>
          <input
            value={newLocation}
            maxLength={80}
            required
            placeholder={t('stations.placeholder')}
            onChange={(event) => setNewLocation(event.target.value)}
          />
        </label>
        <button className="primary-button" type="submit" disabled={busy || !newLocation.trim()}>
          {t('stations.add')} <span aria-hidden="true">+</span>
        </button>
      </form>
    </section>
  )
}

// Belegung: welcher Nutzer ist gerade an welcher Box
function Occupancy({ auth }: { auth: Auth }) {
  const { t, locale } = useI18n()
  const token = auth.session!.token
  const [boxes, setBoxes] = useState<BoxOccupancy[] | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        setBoxes(await api.getBoxOccupancy(token, controller.signal))
        setFailed(false)
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof ApiError && err.status === 401) auth.logout('login.expired')
        else setFailed(true)
      }
    }
    load()
    const timer = window.setInterval(load, 5000)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [token, auth])

  return (
    <section className="admin-panel admin-section" aria-labelledby="occupancy-heading">
      <p className="section-kicker">{t('admin.occKicker')}</p>
      <h2 id="occupancy-heading">{t('admin.occHeading')}</h2>
      {failed && (
        <p className="form-error" role="alert">
          {t('error.generic')}
        </p>
      )}
      {boxes && (
        <ul className="occupancy-list">
          {boxes.map((box) => (
            <li key={box.id} className={`occupancy-item ${box.state === 'Blocked' ? 'is-blocked' : ''}`}>
              <div className="occupancy-box">
                <strong>{slotName(t, box.id)}</strong>
                {box.location && (
                  <span className="spot-location">
                    <PinIcon /> {box.location}
                  </span>
                )}
              </div>
              <div className="occupancy-user">
                {box.username ? (
                  <>
                    <span>{t('admin.occBy', { user: box.username })}</span>
                    <span className="occupancy-since">
                      {box.parkedAt
                        ? t('admin.occParkedSince', { time: formatDateTime(box.parkedAt, locale) })
                        : box.bookedAt && t('admin.occBookedSince', { time: formatDateTime(box.bookedAt, locale) })}
                    </span>
                  </>
                ) : (
                  <span className="occupancy-free">{box.state === 'Blocked' ? t('admin.occBlocked') : t('admin.occFree')}</span>
                )}
              </div>
              <div className="occupancy-state">
                <span>{t(`state.${box.state}`)}</span>
                <span className="occupancy-since">
                  {box.lockOpen ? t('admin.lockOpen') : t('admin.lockClosed')}
                  {!box.isOnline && ` · ${t('status.offline')}`}
                </span>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

// Protokoll: wer hat wann welche Box gebucht, geöffnet und geschlossen
function BoxLog({ auth }: { auth: Auth }) {
  const { t, locale } = useI18n()
  const token = auth.session!.token
  const [events, setEvents] = useState<BoxEvent[] | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        setEvents(await api.getBoxEvents(token, 200, controller.signal))
        setFailed(false)
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof ApiError && err.status === 401) auth.logout('login.expired')
        else setFailed(true)
      }
    }
    load()
    const timer = window.setInterval(load, 5000)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [token, auth])

  const [filter, setFilter] = useState('')
  const dateFormat = new Intl.DateTimeFormat(locale, { day: '2-digit', month: '2-digit', year: 'numeric' })
  const timeFormat = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
  const query = filter.trim().toLowerCase()
  const shown = (events ?? []).filter((e) => !query || (e.username ?? '').toLowerCase().includes(query))

  // Ein Satz pro Ereignis, z. B. "anna hat Stellplatz 1 (Hackathon Halle) am 29.09.2026 um 10:41:07 gebucht."
  function sentence(e: BoxEvent): string {
    const when = new Date(e.timestamp)
    const slot = e.location ? `${slotName(t, e.slotId)} (${e.location})` : slotName(t, e.slotId)
    return t(`log.${e.type}`, {
      user: e.username ?? t('log.unknownUser'),
      slot,
      date: dateFormat.format(when),
      time: timeFormat.format(when),
    })
  }

  return (
    <details className="admin-panel admin-section log-section">
      <summary>
        <span>
          <span className="section-kicker">{t('admin.logKicker')}</span>
          <span className="log-summary-title">{t('admin.logHeading')}</span>
        </span>
        <span className="log-summary-count">{t('admin.logCount', { count: events?.length ?? 0 })}</span>
      </summary>
      <p className="chart-note">{t('admin.logText')}</p>
      <label className="form-field log-filter">
        <span>{t('admin.logFilter')}</span>
        <input type="search" value={filter} onChange={(event) => setFilter(event.target.value)} autoComplete="off" />
      </label>
      {failed && (
        <p className="form-error" role="alert">
          {t('error.generic')}
        </p>
      )}
      {events && shown.length === 0 && <p className="alerts-empty">{t('admin.logEmpty')}</p>}
      {shown.length > 0 && (
        <ol className="event-log">
          {shown.map((e) => (
            <li key={e.id} className={e.type === 'BikeRemoved' ? 'is-alarm' : undefined}>
              <time dateTime={e.timestamp}>
                {dateFormat.format(new Date(e.timestamp))}
                <br />
                {timeFormat.format(new Date(e.timestamp))}
              </time>
              <span className="event-text">{sentence(e)}</span>
              <span className={`event-lock ${e.lockOpen ? 'is-open' : ''}`}>
                {e.lockOpen ? t('admin.lockOpen') : t('admin.lockClosed')}
              </span>
            </li>
          ))}
        </ol>
      )}
    </details>
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

function AdminArea({ auth, data }: Omit<AdminPageProps, 'demoAccounts'>) {
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

      <Occupancy auth={auth} />

      <Stations auth={auth} />

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

      <BlockedBoxes auth={auth} />

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

      <BoxLog auth={auth} />
    </>
  )
}
