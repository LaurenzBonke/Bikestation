import type { Route } from '../useRoute'

type HeaderProps = {
  connected: boolean
  route: Route
}

export default function Header({ connected, route }: HeaderProps) {
  return (
    <header className="topbar">
      <a className="brand" href="#" aria-label="Smart Bikestation Startseite">
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
      <nav className="top-nav" aria-label="Hauptnavigation">
        <a
          className={`nav-link ${route === 'overview' ? 'active' : ''}`}
          href="#"
          aria-current={route === 'overview' ? 'page' : undefined}
        >
          Übersicht
        </a>
        <a
          className={`nav-link ${route === 'admin' ? 'active' : ''}`}
          href="#admin"
          aria-current={route === 'admin' ? 'page' : undefined}
        >
          Admin
        </a>
      </nav>
      <div className={`topbar-note ${connected ? '' : 'is-offline'}`} role="status">
        <span className="status-dot" aria-hidden="true"></span>
        {connected ? 'Live verbunden' : 'Keine Verbindung'}
      </div>
    </header>
  )
}
