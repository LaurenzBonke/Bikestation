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
  isOnline: boolean
  possibleTampering: boolean
  hasAnomaly: boolean
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
  score: number | null
}

export type HourlyOccupancy = {
  hour: number
  occupancyPercent: number
  readings: number
}

export type SlotStatistics = {
  slotId: number
  name: string
  occupancyPercent: number
  tamperEvents: number
  anomalyEvents: number
}

export type Statistics = {
  periodDays: number
  totalReadings: number
  occupancyPercent: number
  busiestHour: number | null
  tamperEvents: number
  anomalyEvents: number
  openAlerts: number
  occupancyByHour: HourlyOccupancy[]
  slots: SlotStatistics[]
}

export type LoginResponse = {
  token: string
  expiresAt: string
  username: string
}

// Fehler mit HTTP-Status, damit z. B. 401 (Token abgelaufen) gezielt behandelt werden kann
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(`/api${path}`, { ...options, headers })
  if (!response.ok) {
    throw new ApiError(response.status, await errorMessage(response))
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

async function errorMessage(response: Response): Promise<string> {
  if (response.status === 429) return 'Zu viele Versuche. Bitte eine Minute warten.'
  try {
    const body = (await response.json()) as { title?: string }
    if (body.title) return body.title
  } catch {
    // Antwort ohne JSON-Body
  }
  return `API-Fehler ${response.status}`
}

export const api = {
  getSlots: (signal?: AbortSignal) => request<Slot[]>('/slots', { signal }),
  getAlerts: (signal?: AbortSignal) => request<Alert[]>('/alerts', { signal }),
  getStatistics: (days: number, signal?: AbortSignal) => request<Statistics>(`/statistics?days=${days}`, { signal }),

  login: (username: string, password: string) =>
    request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }),
  getMe: (token: string) => request<{ username: string }>('/auth/me', {}, token),
  resolveAlert: (id: number, token: string) => request<void>(`/alerts/${id}/resolve`, { method: 'POST' }, token),
  generateDemoData: (days: number, token: string) =>
    request<{ readings: number }>(`/demo/generate?days=${days}`, { method: 'POST' }, token),
}
