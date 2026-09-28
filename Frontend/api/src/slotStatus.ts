import type { Alert, Slot } from './api'
import type { TranslationKey, useI18n } from './i18n'

type Translate = ReturnType<typeof useI18n>['t']

export type DisplayStatus = 'free' | 'taken' | 'offline' | 'unknown'

// Ein Platz, dessen ESP32 sich nicht mehr meldet, zählt weder als frei noch als belegt
export function displayStatus(slot: Slot): DisplayStatus {
  if (slot.status === 'Unknown') return 'unknown'
  if (!slot.isOnline) return 'offline'
  return slot.status === 'Free' ? 'free' : 'taken'
}

export const statusKey: Record<DisplayStatus, TranslationKey> = {
  free: 'status.free',
  taken: 'status.taken',
  offline: 'status.offline',
  unknown: 'status.unknown',
}

// Stellplatz-Namen kommen übersetzt aus dem Frontend, nicht aus der (deutschen) Datenbank
export function slotName(t: Translate, id: number): string {
  return t('slot.name', { id })
}

// Begründungen, die Backend und Python-KI in Meldungen schreiben (deutsch) -> Übersetzungsschlüssel
const reasonKeys: Record<string, TranslationKey> = {
  Vibration: 'reason.vibration',
  'ungewöhnlicher Druck': 'reason.pressure',
  'ungewöhnlicher Abstand': 'reason.distance',
  'starke Druckänderung': 'reason.pressureChange',
  'starke Abstandsänderung': 'reason.distanceChange',
  'ungewöhnliche Kombination der Messwerte': 'reason.combination',
}

// Meldungstext in der gewählten Sprache. Die Begründung einer KI-Anomalie steht in Klammern
// in der gespeicherten Meldung; unbekannte Teile bleiben unverändert.
export function alertMessage(t: Translate, alert: Alert): string {
  const slot = slotName(t, alert.slotId)
  switch (alert.type) {
    case 'PossibleTampering':
      return t('alert.tamper', { slot })
    case 'SensorMismatch':
      return t('alert.mismatch', { slot })
    case 'BikeRemoved':
      return t('alert.bikeRemoved', { slot })
    case 'Anomaly': {
      const match = alert.message.match(/\(([^()]*)\)\.?$/)
      if (!match) return t('alert.anomaly', { slot })
      const reason = match[1]
        .split(' + ')
        .map((part) => (reasonKeys[part.trim()] ? t(reasonKeys[part.trim()]) : part.trim()))
        .join(' + ')
      return t('alert.anomalyReason', { slot, reason })
    }
  }
}
