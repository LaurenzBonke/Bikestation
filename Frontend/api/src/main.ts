import './style.css'

type Station = {
  id: number
  name: string
  address: string
  neighborhood: string
  description: string
  opening_hours: string
  spots: boolean[]
}

type Session = { authenticated: boolean; email?: string; csrfToken?: string }

const demoStations: Station[] = [
  { id: 1, name: 'Central Library', address: '128 Civic Plaza', neighborhood: 'Downtown', description: 'Secure bike parking beside the Central Library entrance.', opening_hours: 'Open 24 hours', spots: [true, false, true] },
  { id: 2, name: 'Riverside Market', address: '42 Riverwalk Avenue', neighborhood: 'Riverside', description: 'Bike parking near the north entrance of Riverside Market.', opening_hours: 'Daily, 6:00 AM - 11:00 PM', spots: [true, true, false] },
  { id: 3, name: 'North Campus', address: '9 University Way', neighborhood: 'University District', description: 'Covered bike parking at the North Campus transit stop.', opening_hours: 'Open 24 hours', spots: [false, true, false] },
]

const app = document.querySelector<HTMLDivElement>('#app')!
let stations = demoStations
let session: Session = { authenticated: false }
let apiConnected = false

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!)
}

async function api<T>(action: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body) headers.set('Content-Type', 'application/json')
  if (session.csrfToken && options.method && options.method !== 'GET') headers.set('X-CSRF-Token', session.csrfToken)
  const response = await fetch(`/api.php?action=${action}`, { ...options, headers, credentials: 'same-origin' })
  const body = await response.json() as T & { error?: string }
  if (!response.ok) throw new Error(body.error || 'Request failed. Please try again.')
  return body
}

function header(admin = false): string {
  return `<header class="topbar">
    <a class="brand" href="#overview" aria-label="Pedal home"><span class="brand-mark" aria-hidden="true"><svg viewBox="0 0 40 40" fill="none"><circle cx="11" cy="27" r="6.5"/><circle cx="29" cy="27" r="6.5"/><path d="m11 27 7-12 7 12H11Zm7-12h6m-3 0 8 12M15 11h5"/></svg></span><span class="brand-name">pedal<span>.</span></span></a>
    <nav class="top-nav" aria-label="Main navigation"><a class="nav-link ${admin ? '' : 'active'}" href="/#overview">Find a spot</a><a class="nav-link ${admin ? 'active' : ''}" href="/#admin">Admin</a></nav>
    <div class="topbar-note"><span class="status-dot"></span> Bike parking</div>
  </header>`
}

function renderPublic() {
  const selectedId = Number(new URLSearchParams(location.hash.split('?')[1] ?? '').get('station'))
  const station = stations.find((item) => item.id === selectedId) ?? stations[0]
  const available = station.spots.filter(Boolean).length
  app.innerHTML = `${header()}<main id="overview" class="page-shell">
    <section class="intro-row"><div><p class="eyebrow"><span class="eyebrow-line"></span> SMART BIKE STATION</p><h1>Find your<br class="mobile-break"> parking spot.</h1><p class="intro-copy">See what's free before you roll up.</p></div>
    <label class="station-picker"><span class="picker-label">CHOOSE A STATION</span><span class="select-wrap"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 21s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12Z"/><circle cx="12" cy="9" r="2.3"/></svg><select id="station-select" aria-label="Choose a bike station">${stations.map((s) => `<option value="${s.id}" ${s.id === station.id ? 'selected' : ''}>${escapeHtml(s.name)}</option>`).join('')}</select><svg class="select-chevron" viewBox="0 0 24 24"><path d="m7 10 5 5 5-5"/></svg></span></label></section>
    ${apiConnected ? '' : '<p class="preview-notice">Preview data is showing. Start the PHP API and MySQL to load live station data.</p>'}
    <section class="dashboard-grid" aria-label="Station availability"><div class="availability-panel">
      <div class="panel-heading"><div><p class="section-kicker">RIGHT NOW</p><h2>Available parking</h2></div><span class="live-label"><span></span> Station overview</span></div>
      <div class="availability-summary"><div class="count-block"><span class="available-count">${available}</span><span class="count-total">of ${station.spots.length}<br>spots free</span></div><div class="occupancy-frame" aria-label="${available} of ${station.spots.length} spots available"><span class="frame-label">PARKING BAY</span><div class="status-lights">${station.spots.map((free, i) => `<span class="status-light ${free ? 'is-available' : 'is-occupied'}" aria-label="Spot ${i + 1} ${free ? 'available' : 'occupied'}"></span>`).join('')}</div><div class="frame-caption"><span>AVAILABLE</span><span>OCCUPIED</span></div></div></div>
      <div class="divider"></div><div class="spots-heading"><div><span class="section-kicker">PICK A BAY</span><span class="spots-hint">Individual spot status</span></div><span class="total-spots">${String(station.spots.length).padStart(2, '0')} TOTAL</span></div>
      <div class="spot-list">${station.spots.map((free, i) => `<div class="spot-row"><span class="spot-number">${String(i + 1).padStart(2, '0')}</span><span class="spot-bike" aria-hidden="true"><svg viewBox="0 0 32 24" fill="none"><circle cx="7" cy="16" r="5"/><circle cx="25" cy="16" r="5"/><path d="m7 16 6-10 6 10H7Zm6-10h5m-2 0 9 10"/></svg></span><span class="spot-name">Parking spot ${i + 1}</span><span class="spot-status ${free ? 'free' : 'taken'}"><span></span>${free ? 'Available' : 'Occupied'}</span></div>`).join('')}</div>
      </div><aside id="station-info" class="station-panel"><div class="map-art" aria-hidden="true"><div class="map-block block-one"></div><div class="map-block block-two"></div><div class="map-block block-three"></div><div class="map-block block-four"></div><div class="map-road road-one"></div><div class="map-road road-two"></div><div class="map-road road-three"></div><div class="map-road road-four"></div><div class="map-label map-label-one">${escapeHtml(station.neighborhood.toUpperCase())}</div><div class="map-label map-label-two">BIKE PARKING</div><span class="map-pin"><svg viewBox="0 0 24 24"><path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/></svg></span></div>
      <div class="station-details"><p class="section-kicker">YOUR STATION</p><h2>${escapeHtml(station.name)}</h2><p class="station-address">${escapeHtml(station.address)}</p><p class="station-description">${escapeHtml(station.description)}</p><div class="station-meta"><span class="meta-icon">↗</span><span>${escapeHtml(station.neighborhood)}</span><span class="meta-separator"></span><span>${escapeHtml(station.opening_hours)}</span></div><a class="directions-link" href="https://maps.google.com/?q=${encodeURIComponent(station.address)}" target="_blank" rel="noreferrer">Get directions <svg viewBox="0 0 24 24"><path d="M7 17 17 7M8 7h9v9"/></svg></a></div><div class="station-footer"><span class="footer-signal"></span> Smart parking station</div></aside></section>
    <footer class="page-footer"><span>Make room for the ride.</span><span>Pedal bike parking <span class="footer-star">✳</span></span></footer></main>`
  document.querySelector<HTMLSelectElement>('#station-select')?.addEventListener('change', (event) => {
    location.hash = `overview?station=${(event.currentTarget as HTMLSelectElement).value}`
    renderPublic()
  })
}

function renderLogin(error = '') {
  app.innerHTML = `${header(true)}<main class="admin-shell login-shell"><section class="admin-heading"><p class="eyebrow"><span class="eyebrow-line"></span> RESTRICTED ACCESS</p><h1>Admin sign in</h1><p>Sign in to update station details and parking availability.</p></section><form id="login-form" class="admin-panel login-panel">
    <label class="form-field"><span>Admin email</span><input name="email" type="email" autocomplete="username" required placeholder="admin@example.com"></label><label class="form-field"><span>Password</span><input name="password" type="password" autocomplete="current-password" required placeholder="Enter your password"></label>
    ${error ? `<p class="form-error" role="alert">${escapeHtml(error)}</p>` : ''}<button class="primary-button" type="submit">Sign in <span aria-hidden="true">→</span></button><p class="login-footnote">Administrator access only</p></form></main>`
  document.querySelector<HTMLFormElement>('#login-form')?.addEventListener('submit', async (event) => {
    event.preventDefault()
    const form = event.currentTarget as HTMLFormElement
    const button = form.querySelector<HTMLButtonElement>('button')!
    button.disabled = true
    button.textContent = 'Signing in...'
    try {
      session = await api<Session>('login', { method: 'POST', body: JSON.stringify(Object.fromEntries(new FormData(form))) })
      location.hash = 'admin'
      renderAdmin()
    } catch (error) {
      renderLogin(error instanceof Error ? error.message : 'Unable to sign in.')
    }
  })
}

function renderAdmin(message = '') {
  if (!session.authenticated) { renderLogin(message); return }
  const selectedId = Number(new URLSearchParams(location.hash.split('?')[1] ?? '').get('station'))
  const station = stations.find((s) => s.id === selectedId) ?? stations[0]
  app.innerHTML = `${header(true)}<main id="admin" class="admin-shell"><section class="admin-heading"><p class="eyebrow"><span class="eyebrow-line"></span> STATION MANAGEMENT</p><h1>Good to see you.</h1><p>Update station information and keep availability current.</p></section>
    <section class="admin-toolbar"><label class="station-picker"><span class="picker-label">EDITING STATION</span><span class="select-wrap"><svg viewBox="0 0 24 24"><path d="M12 21s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12Z"/><circle cx="12" cy="9" r="2.3"/></svg><select id="admin-station-select" aria-label="Choose a station to edit">${stations.map((s) => `<option value="${s.id}" ${s.id === station.id ? 'selected' : ''}>${escapeHtml(s.name)}</option>`).join('')}</select><svg class="select-chevron" viewBox="0 0 24 24"><path d="m7 10 5 5 5-5"/></svg></span></label><div class="admin-account"><span class="account-avatar">${escapeHtml((session.email ?? 'A')[0].toUpperCase())}</span><span>${escapeHtml(session.email ?? 'Administrator')}</span><button id="logout-button" class="text-button" type="button">Sign out</button></div></section>
    <form id="station-form" class="admin-layout"><section class="admin-panel station-edit-panel"><div class="admin-panel-heading"><div><p class="section-kicker">STATION PROFILE</p><h2>Station information</h2></div><span class="edit-mark" aria-hidden="true">✳</span></div><div class="form-grid">
      <label class="form-field"><span>Station name</span><input name="name" maxlength="120" required value="${escapeHtml(station.name)}"></label><label class="form-field"><span>Neighborhood</span><input name="neighborhood" maxlength="120" value="${escapeHtml(station.neighborhood)}"></label><label class="form-field form-wide"><span>Street address</span><input name="address" maxlength="255" required value="${escapeHtml(station.address)}"></label><label class="form-field form-wide"><span>Station description</span><textarea name="description" rows="3" maxlength="2000">${escapeHtml(station.description)}</textarea></label><label class="form-field form-wide"><span>Opening hours</span><input name="openingHours" maxlength="120" value="${escapeHtml(station.opening_hours)}"></label>
    </div></section><section class="admin-panel spot-edit-panel"><div class="admin-panel-heading"><div><p class="section-kicker">LIVE CAPACITY</p><h2>Parking spots <span class="spot-count-pill">${station.spots.length}</span></h2></div><span class="spot-summary"><strong>${station.spots.filter(Boolean).length}</strong> available</span></div><p class="editor-hint">Set each spot to available or occupied. Changes appear on the public page after saving.</p><div class="admin-spot-list">${station.spots.map((free, i) => `<label class="admin-spot-row"><span class="admin-spot-icon"><svg viewBox="0 0 32 24" fill="none"><circle cx="7" cy="16" r="5"/><circle cx="25" cy="16" r="5"/><path d="m7 16 6-10 6 10H7Zm6-10h5m-2 0 9 10"/></svg></span><span class="admin-spot-label"><strong>Parking spot ${i + 1}</strong><small>Bay ${String(i + 1).padStart(2, '0')}</small></span><span class="toggle-state">${free ? 'Available' : 'Occupied'}</span><input class="spot-toggle" type="checkbox" name="spot-${i}" aria-label="Parking spot ${i + 1} available" ${free ? 'checked' : ''}><span class="toggle-track" aria-hidden="true"><span></span></span></label>`).join('')}</div></section>
    <div class="save-bar"><span class="save-message" role="status">${escapeHtml(message || 'Changes are saved to the station database.')}</span><button class="primary-button" type="submit">Save changes <span aria-hidden="true">→</span></button></div></form><footer class="page-footer"><span>Make room for the ride.</span><a href="/#overview">View public station page ↗</a></footer></main>`

  document.querySelector<HTMLSelectElement>('#admin-station-select')?.addEventListener('change', (event) => {
    location.hash = `admin?station=${(event.currentTarget as HTMLSelectElement).value}`
    renderAdmin()
  })
  document.querySelector<HTMLButtonElement>('#logout-button')?.addEventListener('click', async () => {
    try { await api('logout', { method: 'POST', body: '{}' }) } finally {
      session = { authenticated: false }
      renderLogin()
    }
  })
  document.querySelectorAll<HTMLInputElement>('.spot-toggle').forEach((toggle) => toggle.addEventListener('change', () => {
    const row = toggle.closest('.admin-spot-row')
    const label = row?.querySelector('.toggle-state')
    if (label) label.textContent = toggle.checked ? 'Available' : 'Occupied'
    const count = document.querySelector<HTMLElement>('.spot-summary strong')
    if (count) count.textContent = String([...document.querySelectorAll<HTMLInputElement>('.spot-toggle')].filter((input) => input.checked).length)
  }))
  document.querySelector<HTMLFormElement>('#station-form')?.addEventListener('submit', async (event) => {
    event.preventDefault()
    const form = event.currentTarget as HTMLFormElement
    const button = form.querySelector<HTMLButtonElement>('.primary-button')!
    const data = new FormData(form)
    const updated: Station = {
      ...station,
      name: String(data.get('name') ?? '').trim(),
      neighborhood: String(data.get('neighborhood') ?? '').trim(),
      address: String(data.get('address') ?? '').trim(),
      description: String(data.get('description') ?? '').trim(),
      opening_hours: String(data.get('openingHours') ?? '').trim(),
      spots: station.spots.map((_, i) => (form.elements.namedItem(`spot-${i}`) as HTMLInputElement).checked),
    }
    button.disabled = true
    button.textContent = 'Saving...'
    try {
      const result = await api<{ station: Station }>('station', { method: 'PUT', body: JSON.stringify(updated) })
      stations = stations.map((item) => item.id === result.station.id ? result.station : item)
      renderAdmin('Changes saved successfully.')
    } catch (error) {
      button.disabled = false
      button.innerHTML = 'Save changes <span aria-hidden="true">→</span>'
      const status = form.querySelector<HTMLElement>('.save-message')
      if (status) { status.textContent = error instanceof Error ? error.message : 'Unable to save changes.'; status.classList.add('form-error') }
    }
  })
}

async function initialize() {
  try {
    const result = await api<{ stations: Station[] }>('stations')
    if (result.stations.length) { stations = result.stations; apiConnected = true }
  } catch { apiConnected = false }
  try { session = await api<Session>('session') } catch { session = { authenticated: false } }
  if (location.hash.startsWith('#admin')) renderAdmin()
  else renderPublic()
}

window.addEventListener('hashchange', () => location.hash.startsWith('#admin') ? renderAdmin() : renderPublic())
void initialize()