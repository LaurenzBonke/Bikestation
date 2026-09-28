import type { Slot } from './api'

export type DisplayStatus = 'free' | 'taken' | 'offline' | 'unknown'

// Ein Platz, dessen ESP32 sich nicht mehr meldet, zählt weder als frei noch als belegt
export function displayStatus(slot: Slot): DisplayStatus {
  if (slot.status === 'Unknown') return 'unknown'
  if (!slot.isOnline) return 'offline'
  return slot.status === 'Free' ? 'free' : 'taken'
}

export const statusText: Record<DisplayStatus, string> = {
  free: 'Frei',
  taken: 'Belegt',
  offline: 'Offline',
  unknown: 'Keine Daten',
}
