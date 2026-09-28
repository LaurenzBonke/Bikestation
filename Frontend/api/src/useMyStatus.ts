import { useCallback, useEffect, useState } from 'react'
import { ApiError, api, type Me } from './api'
import type { Auth } from './useAuth'

// Wie oft Box-Zustand und Meldungen des Nutzers aktualisiert werden
const POLL_INTERVAL_MS = 2000

export type MyStatus = {
  me: Me | null
  refresh: () => void
}

// Hält Box, Meldungen und Verlauf des angemeldeten Nutzers aktuell – auch für das Alarm-Banner auf jeder Seite
export function useMyStatus(auth: Auth): MyStatus {
  const [me, setMe] = useState<Me | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const token = auth.session?.token ?? null
  const { logout } = auth

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), [])

  useEffect(() => {
    if (!token) {
      setMe(null)
      return
    }
    const controller = new AbortController()

    async function load() {
      try {
        setMe(await api.getMyStatus(token!, controller.signal))
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) logout('login.expired')
        // andere Fehler (z. B. kurz keine Verbindung): letzten Stand behalten
      }
    }

    void load()
    const timer = window.setInterval(load, POLL_INTERVAL_MS)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [token, logout, refreshKey])

  return { me, refresh }
}
