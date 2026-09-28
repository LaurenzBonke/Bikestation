import { useCallback, useEffect, useState } from 'react'
import { api, type Box } from './api'

const POLL_INTERVAL_MS = 2000

// Zustand aller Boxen; mit Token wird zusätzlich markiert, welche Box dem Nutzer gehört
export function useBoxes(token: string | null): { boxes: Box[]; loaded: boolean; refresh: () => void } {
  const [boxes, setBoxes] = useState<Box[]>([])
  const [loaded, setLoaded] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        setBoxes(await api.getBoxes(token, controller.signal))
        setLoaded(true)
      } catch {
        // Verbindungsfehler zeigt bereits die Kopfzeile an
      }
    }

    void load()
    const timer = window.setInterval(load, POLL_INTERVAL_MS)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [token, refreshKey])

  return { boxes, loaded, refresh }
}
