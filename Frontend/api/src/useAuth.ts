import { useCallback, useEffect, useState } from 'react'
import { api, type LoginResponse } from './api'
import type { TranslationKey } from './i18n'

// sessionStorage: Login bleibt beim Neuladen erhalten, ist aber weg, sobald der Tab geschlossen wird
const STORAGE_KEY = 'bikestation-auth'

export type Auth = {
  session: LoginResponse | null
  login: (username: string, password: string) => Promise<void>
  register: (username: string, password: string) => Promise<void>
  logout: (reason?: TranslationKey) => void
  // Übersetzungsschlüssel, warum abgemeldet wurde (z. B. Sitzung abgelaufen)
  logoutReason: TranslationKey | null
}

function loadSession(): LoginResponse | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as LoginResponse
    return new Date(session.expiresAt) > new Date() && session.role ? session : null
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
  const [logoutReason, setLogoutReason] = useState<TranslationKey | null>(null)

  const logout = useCallback((reason?: TranslationKey) => {
    saveSession(null)
    setSession(null)
    setLogoutReason(reason ?? null)
  }, [])

  const start = useCallback((result: LoginResponse) => {
    saveSession(result)
    setSession(result)
    setLogoutReason(null)
  }, [])

  const login = useCallback(
    async (username: string, password: string) => start(await api.login(username, password)),
    [start],
  )

  const register = useCallback(
    async (username: string, password: string) => start(await api.register(username, password)),
    [start],
  )

  // Gespeicherten Token beim Start einmal vom Backend prüfen lassen
  useEffect(() => {
    if (!session) return
    api.getMe(session.token).catch(() => logout('login.expired'))
  }, []) // nur beim Start

  // Automatisch abmelden, wenn der Token abläuft
  useEffect(() => {
    if (!session) return
    const msLeft = new Date(session.expiresAt).getTime() - Date.now()
    const timer = window.setTimeout(
      () => logout('login.expired'),
      Math.max(msLeft, 0),
    )
    return () => window.clearTimeout(timer)
  }, [session, logout])

  return { session, login, register, logout, logoutReason }
}
