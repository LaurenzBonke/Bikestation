import { useCallback, useEffect, useState } from 'react'
import { api, type DemoInfo, type VirtualBox } from './api'

const NO_DEMO: DemoInfo = { virtualStation: false, accounts: [] }
const STATION_POLL_MS = 1000

// Läuft der Server im Demo-Modus? Einmal beim Start gefragt – ohne Antwort gilt: keine Demo
export function useDemoInfo(): DemoInfo {
  const [info, setInfo] = useState<DemoInfo>(NO_DEMO)
  useEffect(() => {
    api.getDemoInfo().then(setInfo, () => setInfo(NO_DEMO))
  }, [])
  return info
}

// Zustand der virtuellen Station, jede Sekunde neu (die Station misst ebenfalls jede Sekunde)
export function useVirtualStation(): { boxes: VirtualBox[]; failed: boolean; refresh: () => void } {
  const [boxes, setBoxes] = useState<VirtualBox[]>([])
  const [failed, setFailed] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        setBoxes(await api.getVirtualStation(controller.signal))
        setFailed(false)
      } catch {
        if (!controller.signal.aborted) setFailed(true)
      }
    }

    void load()
    const timer = window.setInterval(load, STATION_POLL_MS)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [refreshKey])

  return { boxes, failed, refresh }
}
