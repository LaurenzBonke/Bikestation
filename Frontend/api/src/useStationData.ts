import { useCallback, useEffect, useState } from 'react'
import { api, type Alert, type Slot } from './api'

// Wie oft das Dashboard neue Daten von der API holt
const POLL_INTERVAL_MS = 3000

export type StationData = {
  slots: Slot[]
  alerts: Alert[]
  connected: boolean
  loading: boolean
  lastFetched: Date | null
  refresh: () => void
}

// adminToken: nur Admins laden die offenen Meldungen mit, normale Nutzer sehen sie nicht
export function useStationData(adminToken: string | null = null): StationData {
  const [slots, setSlots] = useState<Slot[]>([])
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [connected, setConnected] = useState(true)
  const [loading, setLoading] = useState(true)
  const [lastFetched, setLastFetched] = useState<Date | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const [newSlots, newAlerts] = await Promise.all([
          api.getSlots(controller.signal),
          adminToken ? api.getAlerts(adminToken, controller.signal) : Promise.resolve([]),
        ])
        setSlots(newSlots)
        setAlerts(newAlerts)
        setConnected(true)
        setLastFetched(new Date())
      } catch {
        if (!controller.signal.aborted) {
          setConnected(false)
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()
    const timer = window.setInterval(load, POLL_INTERVAL_MS)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [refreshKey, adminToken])

  return { slots, alerts, connected, loading, lastFetched, refresh }
}
