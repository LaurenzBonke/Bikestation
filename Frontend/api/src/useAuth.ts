import { useCallback, useEffect, useState } from 'react'
import { api, type LoginResponse } from './api'

// sessionStorage: Login bleibt beim Neuladen erhalten, ist aber weg, sobald der Tab geschlossen wird
const STORAGE_KEY = 'bikestation-auth'

export type Auth = {
  session: LoginResponse | null
  login: (username: string, password: string) => Promise<void>
  logout: (reason?: string) => void
  logoutReason: string
}

function loadSession(): LoginResponse | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as LoginResponse
    return new Date(session.expiresAt) > new Date() ? session : null
  } catch {
    return null
  }
}

function saveSession(session: LoginResponse | null) {
  try {
    if (session) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
    else sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // Speicher nicht verfügbar (z. B. privater Modus) – Login gilt dann nur bis zum Neuladen
  }
}

export function useAuth(): Auth {
  const [session, setSession] = useState<LoginResponse | null>(loadSession)
  const [logoutReason, setLogoutReason] = useState('')

  const logout = useCallback((reason = '') => {
    saveSession(null)
    setSession(null)
    setLogoutReason(reason)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
    const result = await api.login(username, password)
    saveSession(result)
    setSession(result)
    setLogoutReason('')
  }, [])

  // Gespeicherten Token beim Start einmal vom Backend prüfen lassen
  useEffect(() => {
    if (!session) return
    api.getMe(session.token).catch(() => logout('Deine Sitzung ist abgelaufen. Bitte erneut anmelden.'))
  }, []) // nur beim Start

  // Automatisch abmelden, wenn der Token abläuft
  useEffect(() => {
    if (!session) return
    const msLeft = new Date(session.expiresAt).getTime() - Date.now()
    const timer = window.setTimeout(
      () => logout('Deine Sitzung ist abgelaufen. Bitte erneut anmelden.'),
      Math.max(msLeft, 0),
    )
    return () => window.clearTimeout(timer)
  }, [session, logout])

  return { session, login, logout, logoutReason }
}
