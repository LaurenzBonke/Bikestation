// Typen entsprechen den DTOs der C#-API (api/bikestation/bikestation/Dtos)

export type SlotStatus = 'Unknown' | 'Free' | 'Occupied'

export type SensorReading = {
  pressure: number
  distance: number
  vibration: boolean
  occupied: boolean
  timestamp: string
}

export type Slot = {
  id: number
  name: string
  status: SlotStatus
  statusText: string
  lastUpdated: string | null
  possibleTampering: boolean
  latestReading: SensorReading | null
}

export type AlertSeverity = 'Info' | 'Warning' | 'Critical'

export type Alert = {
  id: number
  slotId: number
  type: 'PossibleTampering' | 'SensorMismatch' | 'Anomaly'
  severity: AlertSeverity
  message: string
  timestamp: string
  resolved: boolean
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`/api${path}`, options)
  if (!response.ok) {
    throw new Error(`API-Fehler ${response.status}`)
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export const api = {
  getSlots: (signal?: AbortSignal) => request<Slot[]>('/slots', { signal }),
  getAlerts: (signal?: AbortSignal) => request<Alert[]>('/alerts', { signal }),
}
