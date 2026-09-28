import type { Route } from '../useRoute'
import { LANGUAGES, useI18n, type Language } from '../i18n'
import { useTheme, type Theme } from '../useTheme'

type HeaderProps = {
  connected: boolean
  route: Route
}

const THEMES: Theme[] = ['system', 'light', 'dark']
const THEME_ICON: Record<Theme, string> = { system: '◐', light: '☀', dark: '☾' }

export default function Header({ connected, route }: HeaderProps) {
  const { t, language, setLanguage } = useI18n()
  const [theme, setTheme] = useTheme()

  return (
    <header className="topbar">
      <a className="brand" href="#" aria-label={t('nav.home')}>
        <span className="brand-mark" aria-hidden="true">
          <svg viewBox="0 0 40 40" fill="none">
            <circle cx="11" cy="27" r="6.5" />
            <circle cx="29" cy="27" r="6.5" />
            <path d="m11 27 7-12 7 12H11Zm7-12h6m-3 0 8 12M15 11h5" />
          </svg>
        </span>
        <span className="brand-name">
          bikestation<span>.</span>
        </span>
      </a>
      <nav className="top-nav" aria-label={t('nav.main')}>
        <a
          className={`nav-link ${route === 'overview' ? 'active' : ''}`}
          href="#"
          aria-current={route === 'overview' ? 'page' : undefined}
        >
          {t('nav.overview')}
        </a>
        <a
          className={`nav-link ${route === 'statistics' ? 'active' : ''}`}
          href="#statistik"
          aria-current={route === 'statistics' ? 'page' : undefined}
        >
          {t('nav.statistics')}
        </a>
        <a
          className={`nav-link ${route === 'admin' ? 'active' : ''}`}
          href="#admin"
          aria-current={route === 'admin' ? 'page' : undefined}
        >
          {t('nav.admin')}
        </a>
      </nav>
      <div className="topbar-tools">
        <div className={`topbar-note ${connected ? '' : 'is-offline'}`} role="status">
          <span className="status-dot" aria-hidden="true"></span>
          {connected ? t('nav.connected') : t('nav.disconnected')}
        </div>
        <label className="compact-select">
          <span className="visually-hidden">{t('settings.language')}</span>
          <select value={language} onChange={(e) => setLanguage(e.target.value as Language)}>
            {LANGUAGES.map((l) => (
              <option key={l.code} value={l.code} lang={l.code}>
                {l.code.toUpperCase()} – {l.label}
              </option>
            ))}
          </select>
        </label>
        <label className="compact-select">
          <span className="visually-hidden">{t('settings.theme')}</span>
          <select value={theme} onChange={(e) => setTheme(e.target.value as Theme)}>
            {THEMES.map((value) => (
              <option key={value} value={value}>
                {THEME_ICON[value]} {t(`theme.${value}`)}
              </option>
            ))}
          </select>
        </label>
      </div>
    </header>
  )
}
