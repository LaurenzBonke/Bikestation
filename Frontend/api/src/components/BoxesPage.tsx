import { useEffect, useRef, useState } from 'react'
import type React from 'react'
import { selectedBoxId } from '../useRoute'
import { PinIcon } from './Dashboard'
import { ApiError, api, type Box, type BoxState, type MyParking, type ParkingHistory } from '../api'
import type { Auth } from '../useAuth'
import type { MyStatus } from '../useMyStatus'
import { useBoxes } from '../useBoxes'
import { formatDateTime, formatTime } from '../format'
import { slotName } from '../slotStatus'
import { useI18n, type TranslationKey } from '../i18n'
import DemoStation from './DemoStation'

type Props = { auth: Auth; status: MyStatus; virtualStation: boolean }

const STEPS: { state: BoxState; label: TranslationKey }[] = [
  { state: 'OpenForParking', label: 'my.step1' },
  { state: 'Locked', label: 'my.step2' },
  { state: 'OpenForPickup', label: 'my.step3' },
]

// Farbe/Symbol je Box-Zustand – immer zusammen mit Text, nie nur Farbe
const stateClass: Record<BoxState, string> = {
  Free: 'free',
  OpenForParking: 'open',
  Locked: 'taken',
  OpenForPickup: 'open',
  Blocked: 'blocked',
}

function errorKey(error: unknown): TranslationKey {
  if (!(error instanceof ApiError)) return 'error.generic'
  if (error.problemType === 'AlreadyHasBox') return 'error.alreadyHasBox'
  if (error.problemType === 'Offline') return 'error.offline'
  if (error.status === 403) return 'error.notAllowed'
  if (error.status === 409) return 'error.wrongState'
  return 'error.generic'
}

export default function BoxesPage({ auth, status, virtualStation }: Props) {
  const { t } = useI18n()
  const token = auth.session?.token ?? null
  const { boxes, loaded, refresh } = useBoxes(token)
  const [busy, setBusy] = useState<number | null>(null)
  const [error, setError] = useState<TranslationKey | null>(null)
  const parking = status.me?.parking ?? null
  const selectedId = selectedBoxId()
  const selectedRef = useRef<HTMLLIElement>(null)

  // Aus der Übersicht kommend: zur gewählten Box scrollen
  useEffect(() => {
    if (loaded && selectedRef.current) {
      selectedRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' })
      selectedRef.current.querySelector<HTMLElement>('button, h3')?.focus({ preventScroll: true })
    }
  }, [loaded, selectedId])

  async function run(boxId: number, action: (id: number, token: string) => Promise<void>) {
    if (!token) return
    setBusy(boxId)
    setError(null)
    try {
      await action(boxId, token)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) auth.logout('login.expired')
      else setError(errorKey(err))
    } finally {
      setBusy(null)
      refresh()
      status.refresh()
    }
  }

  return (
    <main id="main" className="page-shell" tabIndex={-1}>
      <section className="intro-row">
        <div>
          <p className="eyebrow">
            <span className="eyebrow-line" aria-hidden="true"></span> {t('boxes.eyebrow')}
          </p>
          <h1>{t('boxes.title')}</h1>
          <p className="intro-copy">{t('boxes.intro')}</p>
        </div>
      </section>

      {error && (
        <p className="error-notice" role="alert">
          {t(error)}
        </p>
      )}

      {!auth.session && (
        <section className="admin-panel admin-section login-cta">
          <p>{t('boxes.loginNeeded')}</p>
          <a className="primary-button" href="#konto">
            {t('boxes.loginButton')} <span aria-hidden="true">→</span>
          </a>
        </section>
      )}

      {parking && (
        <MyParkingCard
          parking={parking}
          busy={busy === parking.slotId}
          onCancel={() => run(parking.slotId, api.cancelBox)}
          onPickup={() => run(parking.slotId, api.pickupBox)}
        />
      )}

      <section className="admin-panel admin-section" aria-labelledby="boxes-heading">
        <p className="section-kicker">{t('boxes.eyebrow')}</p>
        <h2 id="boxes-heading">{t('boxes.listHeading')}</h2>
        {loaded && boxes.length === 0 && <p className="chart-note">{t('boxes.none')}</p>}
        <ul className="box-grid">
          {boxes.map((box) => (
            <BoxCard
              key={box.id}
              box={box}
              selected={box.id === selectedId}
              cardRef={box.id === selectedId ? selectedRef : undefined}
              canBook={!!auth.session && !parking && box.state === 'Free' && box.isOnline}
              busy={busy === box.id}
              onBook={() => run(box.id, api.bookBox)}
            />
          ))}
        </ul>
      </section>

      {virtualStation && (
        <DemoStation
          onChange={() => {
            refresh()
            status.refresh()
          }}
        />
      )}

      {status.me && status.me.history.length > 0 && (
        <section className="admin-panel admin-section" aria-labelledby="history-heading">
          <h2 id="history-heading">{t('history.heading')}</h2>
          <History history={status.me.history} />
        </section>
      )}
    </main>
  )


}

function History({ history }: { history: ParkingHistory[] }) {
  const { t, locale } = useI18n()
  return (
    <ul className="history-list">
      {history.map((h, i) => (
        <li key={i}>
          <strong>{slotName(t, h.slotId)}</strong>
          <span>{formatDateTime(h.bookedAt, locale)}</span>
          <span className={`history-reason ${h.endReason === 'BikeRemoved' ? 'is-alarm' : ''}`}>
            {h.endReason ? t(`end.${h.endReason}`) : '–'}
          </span>
        </li>
      ))}
    </ul>
  )
}

function BoxCard({
  box,
  selected,
  cardRef,
  canBook,
  busy,
  onBook,
}: {
  box: Box
  selected: boolean
  cardRef?: React.Ref<HTMLLIElement>
  canBook: boolean
  busy: boolean
  onBook: () => void
}) {
  const { t } = useI18n()
  const name = slotName(t, box.id)
  return (
    <li ref={cardRef} className={`box-card ${box.isMine ? 'is-mine' : ''} ${selected ? 'is-selected' : ''}`}>
      <div className="box-card-head">
        <span className="box-number" aria-hidden="true">
          {box.id}
        </span>
        <div>
          <h3 tabIndex={-1}>{name}</h3>
          {box.location && (
            <span className="spot-location">
              <PinIcon /> {box.location}
            </span>
          )}
          {box.isMine && <span className="mine-badge">{t('boxes.mine')}</span>}
        </div>
      </div>
      <p className={`box-state ${stateClass[box.state]}`}>
        <span aria-hidden="true"></span>
        {t(`state.${box.state}`)}
      </p>
      {!box.isOnline && <p className="offline-note">{t('boxes.offline')}</p>}
      {canBook && (
        <button className="primary-button" type="button" disabled={busy} onClick={onBook} aria-label={t('boxes.bookAria', { slot: name })}>
          {busy ? t('boxes.opening') : t('boxes.book')} <span aria-hidden="true">→</span>
        </button>
      )}
    </li>
  )
}

function MyParkingCard({
  parking,
  busy,
  onCancel,
  onPickup,
}: {
  parking: MyParking
  busy: boolean
  onCancel: () => void
  onPickup: () => void
}) {
  const { t, locale } = useI18n()
  const now = useNow()
  const stepIndex = STEPS.findIndex((s) => s.state === parking.state)
  const remaining = parking.deadlineAt ? Math.max(0, new Date(parking.deadlineAt).getTime() - now) : null

  return (
    <section className={`admin-panel admin-section my-box state-${stateClass[parking.state]}`} aria-labelledby="my-box-heading" aria-live="polite">
      <p className="section-kicker">{t('my.kicker')}</p>
      <h2 id="my-box-heading">{t('my.heading', { slot: slotName(t, parking.slotId) })}</h2>

      <ol className="steps">
        {STEPS.map((step, i) => (
          <li key={step.state} className={i < stepIndex ? 'done' : i === stepIndex ? 'current' : ''} aria-current={i === stepIndex ? 'step' : undefined}>
            <span className="step-dot" aria-hidden="true">
              {i < stepIndex ? '✓' : i + 1}
            </span>
            {t(step.label)}
          </li>
        ))}
      </ol>

      <p className="my-box-state">
        <span className={`lock-icon ${parking.lockOpen ? 'is-open' : ''}`} aria-hidden="true">
          {parking.lockOpen ? '🔓' : '🔒'}
        </span>
        {parking.state === 'OpenForParking' || parking.state === 'Locked' || parking.state === 'OpenForPickup'
          ? t(`my.${parking.state}`)
          : t(`state.${parking.state}`)}
      </p>

      {remaining !== null && (
        <p className="deadline">{t('my.deadline', { time: formatCountdown(remaining) })}</p>
      )}
      {parking.parkedAt && parking.state === 'Locked' && (
        <p className="chart-note">{t('my.parkedSince', { time: formatTime(parking.parkedAt, locale) })}</p>
      )}
      {!parking.isOnline && (
        <p className="error-notice" role="status">
          {t('my.offline')}
        </p>
      )}

      <div className="admin-actions">
        {parking.state === 'OpenForParking' && (
          <button className="secondary-button" type="button" disabled={busy} onClick={onCancel}>
            {t('my.cancel')}
          </button>
        )}
        {parking.state === 'Locked' && (
          <button className="primary-button big-button" type="button" disabled={busy} onClick={onPickup}>
            🔓 {t('my.pickup')}
          </button>
        )}
      </div>
    </section>
  )
}

// Aktuelle Zeit, jede Sekunde neu – für den Countdown
function useNow(): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])
  return now
}

function formatCountdown(ms: number): string {
  const total = Math.ceil(ms / 1000)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}
