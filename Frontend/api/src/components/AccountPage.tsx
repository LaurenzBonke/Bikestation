import { useState, type FormEvent } from 'react'
import { ApiError } from '../api'
import type { Auth } from '../useAuth'
import { useI18n, type TranslationKey } from '../i18n'

type Mode = 'login' | 'register'

const USERNAME_PATTERN = /^[A-Za-z0-9_.-]{3,32}$/

export default function AccountPage({ auth }: { auth: Auth }) {
  const { t } = useI18n()

  if (auth.session) {
    return (
      <main id="main" className="admin-shell login-shell" tabIndex={-1}>
        <section className="admin-heading">
          <p className="eyebrow">
            <span className="eyebrow-line" aria-hidden="true"></span> {t('account.eyebrow')}
          </p>
          <h1>{t('account.signedInAs', { name: auth.session.username })}</h1>
        </section>
        <div className="admin-panel login-panel">
          <a className="primary-button" href="#boxen">
            {t('account.toBoxes')} <span aria-hidden="true">→</span>
          </a>
          <button className="secondary-button" type="button" onClick={() => auth.logout()}>
            {t('admin.logout')}
          </button>
        </div>
      </main>
    )
  }

  return (
    <main id="main" className="admin-shell login-shell" tabIndex={-1}>
      <section className="admin-heading">
        <p className="eyebrow">
          <span className="eyebrow-line" aria-hidden="true"></span> {t('account.eyebrow')}
        </p>
        <h1>{t('account.title')}</h1>
        <p>{t('account.intro')}</p>
      </section>
      <AccountForm auth={auth} />
    </main>
  )
}

function AccountForm({ auth }: { auth: Auth }) {
  const { t } = useI18n()
  const [mode, setMode] = useState<Mode>('login')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [error, setError] = useState<TranslationKey | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (mode === 'register') {
      if (!USERNAME_PATTERN.test(username.trim()) || password.length < 8) {
        setError('account.invalid')
        return
      }
      if (password !== repeat) {
        setError('account.passwordMismatch')
        return
      }
    }

    setSubmitting(true)
    try {
      if (mode === 'login') await auth.login(username.trim(), password)
      else await auth.register(username.trim(), password)
      // Nach dem Anmelden direkt zu den Boxen
      window.location.hash = 'boxen'
    } catch (err) {
      const status = err instanceof ApiError ? err.status : 0
      if (status === 401) setError('login.invalid')
      else if (status === 409) setError('account.usernameTaken')
      else if (status === 400) setError('account.invalid')
      else if (status === 429) setError('error.tooMany')
      else setError('error.generic')
      setSubmitting(false)
    }
  }

  function switchMode(next: Mode) {
    setMode(next)
    setError(null)
    setRepeat('')
  }

  return (
    <form className="admin-panel login-panel" onSubmit={handleSubmit} noValidate>
      <div className="mode-tabs" role="tablist" aria-label={t('account.title')}>
        {(['login', 'register'] as Mode[]).map((m) => (
          <button
            key={m}
            type="button"
            role="tab"
            aria-selected={mode === m}
            className={`period-button ${mode === m ? 'active' : ''}`}
            onClick={() => switchMode(m)}
          >
            {m === 'login' ? t('account.tabLogin') : t('account.tabRegister')}
          </button>
        ))}
      </div>

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
          maxLength={32}
          value={username}
          aria-describedby={mode === 'register' ? 'username-hint' : undefined}
          onChange={(e) => setUsername(e.target.value)}
        />
        {mode === 'register' && (
          <small id="username-hint" className="field-hint">
            {t('account.usernameHint')}
          </small>
        )}
      </label>
      <label className="form-field">
        <span>{t('login.password')}</span>
        <input
          name="password"
          type="password"
          autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
          required
          maxLength={128}
          value={password}
          aria-describedby={mode === 'register' ? 'password-hint' : undefined}
          onChange={(e) => setPassword(e.target.value)}
        />
        {mode === 'register' && (
          <small id="password-hint" className="field-hint">
            {t('account.passwordHint')}
          </small>
        )}
      </label>
      {mode === 'register' && (
        <label className="form-field">
          <span>{t('account.passwordRepeat')}</span>
          <input
            name="password-repeat"
            type="password"
            autoComplete="new-password"
            required
            maxLength={128}
            value={repeat}
            onChange={(e) => setRepeat(e.target.value)}
          />
        </label>
      )}

      {error && (
        <p className="form-error" role="alert">
          {t(error)}
        </p>
      )}
      <button className="primary-button" type="submit" disabled={submitting}>
        {mode === 'login'
          ? submitting
            ? t('login.submitting')
            : t('login.submit')
          : submitting
            ? t('account.registering')
            : t('account.registerSubmit')}{' '}
        <span aria-hidden="true">→</span>
      </button>
    </form>
  )
}
