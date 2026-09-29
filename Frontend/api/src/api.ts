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
  location: string
  status: SlotStatus
  statusText: string
  lastUpdated: string | null
  isOnline: boolean
  boxState: BoxState
  possibleTampering: boolean
  hasAnomaly: boolean
  latestReading: SensorReading | null
}

export type AlertSeverity = 'Info' | 'Warning' | 'Critical'

export type Alert = {
  id: number
  slotId: number
  type: 'PossibleTampering' | 'SensorMismatch' | 'Anomaly' | 'BikeRemoved'
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

export type DailyOccupancy = {
  date: string
  occupancyPercent: number
  readings: number
  tamperEvents: number
  anomalyEvents: number
}

export type ForecastHour = {
  start: string
  hour: number
  occupancyPercent: number | null
  expectedFree: number | null
  readings: number
}

export type Forecast = {
  totalSlots: number
  basedOnDays: number
  hours: ForecastHour[]
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
  occupancyByDay: DailyOccupancy[]
  slots: SlotStatistics[]
}

export type UserRole = 'User' | 'Admin'

export type LoginResponse = {
  token: string
  expiresAt: string
  username: string
  role: UserRole
}

export type BoxState = 'Free' | 'OpenForParking' | 'Locked' | 'OpenForPickup' | 'Blocked'

export type Box = {
  id: number
  name: string
  location: string
  state: BoxState
  lockOpen: boolean
  bikeDetected: boolean
  isOnline: boolean
  isMine: boolean
  stateChangedAt: string | null
}

export type MyParking = {
  parkingId: number
  slotId: number
  state: BoxState
  lockOpen: boolean
  isOnline: boolean
  bookedAt: string
  parkedAt: string | null
  pickupRequestedAt: string | null
  deadlineAt: string | null
}

export type BoxOccupancy = {
  id: number
  name: string
  location: string
  state: BoxState
  lockOpen: boolean
  isOnline: boolean
  username: string | null
  bookedAt: string | null
  parkedAt: string | null
  stateChangedAt: string | null
}

export type BoxEventType =
  | 'Booked'
  | 'Cancelled'
  | 'Parked'
  | 'PickupRequested'
  | 'PickedUp'
  | 'ParkingTimedOut'
  | 'PickupTimedOut'
  | 'BikeRemoved'
  | 'Released'

export type BoxEvent = {
  id: number
  slotId: number
  slotName: string
  location: string
  username: string | null
  type: BoxEventType
  lockOpen: boolean
  timestamp: string
}

export type ParkingEndReason = 'Completed' | 'Cancelled' | 'TimedOut' | 'BikeRemoved'

export type ParkingHistory = {
  slotId: number
  bookedAt: string
  parkedAt: string | null
  endedAt: string | null
  endReason: ParkingEndReason | null
}

export type Me = {
  id: number
  username: string
  role: UserRole
  parking: MyParking | null
  alerts: Alert[]
  history: ParkingHistory[]
}

// Fehler mit HTTP-Status, damit z. B. 401 (Token abgelaufen) gezielt behandelt werden kann
export class ApiError extends Error {
  readonly status: number
  // "type" aus den ProblemDetails, z. B. "AlreadyHasBox" oder "WrongState"
  readonly problemType: string

  constructor(status: number, message: string, problemType = '') {
    super(message)
    this.status = status
    this.problemType = problemType
  }
}

async function request<T>(path: string, options: RequestInit = {}, token?: string | null): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(`/api${path}`, { ...options, headers })
  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(response.status, problem.title ?? `API-Fehler ${response.status}`, problem.type ?? '')
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

async function readProblem(response: Response): Promise<{ title?: string; type?: string }> {
  try {
    return (await response.json()) as { title?: string; type?: string }
  } catch {
    return {}
  }
}

export const api = {
  getSlots: (signal?: AbortSignal) => request<Slot[]>('/slots', { signal }),
  getAlerts: (token: string, signal?: AbortSignal) => request<Alert[]>('/alerts', { signal }, token),
  getStatistics: (days: number, signal?: AbortSignal) => request<Statistics>(`/statistics?days=${days}`, { signal }),
  getForecast: (hours: number, signal?: AbortSignal) => request<Forecast>(`/forecast?hours=${hours}`, { signal }),

  login: (username: string, password: string) =>
    request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }),
  register: (username: string, password: string) =>
    request<LoginResponse>('/auth/register', { method: 'POST', body: JSON.stringify({ username, password }) }),
  getMe: (token: string) => request<{ id: number; username: string; role: UserRole }>('/auth/me', {}, token),
  getMyStatus: (token: string, signal?: AbortSignal) => request<Me>('/me', { signal }, token),
  ackAlert: (id: number, token: string) => request<void>(`/me/alerts/${id}/ack`, { method: 'POST' }, token),

  getBoxes: (token: string | null, signal?: AbortSignal) => request<Box[]>('/boxes', { signal }, token),
  bookBox: (id: number, token: string) => request<void>(`/boxes/${id}/book`, { method: 'POST' }, token),
  cancelBox: (id: number, token: string) => request<void>(`/boxes/${id}/cancel`, { method: 'POST' }, token),
  pickupBox: (id: number, token: string) => request<void>(`/boxes/${id}/pickup`, { method: 'POST' }, token),
  releaseBox: (id: number, token: string) => request<void>(`/boxes/${id}/release`, { method: 'POST' }, token),
  addStation: (location: string, token: string) =>
    request<{ id: number }>('/boxes', { method: 'POST', body: JSON.stringify({ location }) }, token),
  updateStation: (id: number, location: string, token: string) =>
    request<void>(`/boxes/${id}`, { method: 'PUT', body: JSON.stringify({ location }) }, token),
  deleteStation: (id: number, token: string) => request<void>(`/boxes/${id}`, { method: 'DELETE' }, token),
  getBoxOccupancy: (token: string, signal?: AbortSignal) =>
    request<BoxOccupancy[]>('/boxes/occupancy', { signal }, token),
  getBoxEvents: (token: string, limit = 200, signal?: AbortSignal) =>
    request<BoxEvent[]>(`/boxes/events?limit=${limit}`, { signal }, token),
  resolveAlert: (id: number, token: string) => request<void>(`/alerts/${id}/resolve`, { method: 'POST' }, token),
  generateDemoData: (days: number, token: string) =>
    request<{ readings: number }>(`/demo/generate?days=${days}`, { method: 'POST' }, token),
}
