// Zahlen und Zeiten im Format der gewählten Sprache (locale z. B. "de-DE", "en-GB", "nl-NL")

export function formatTime(value: string | Date | null, locale: string): string {
  if (!value) return '–'
  return new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date(value))
}

export function formatDateTime(value: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }).format(
    new Date(value),
  )
}

export function formatDay(date: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { weekday: 'short', day: '2-digit', month: '2-digit' }).format(
    new Date(`${date}T12:00:00`),
  )
}

export function formatNumber(value: number, locale: string): string {
  return value.toLocaleString(locale)
}

export function hourLabel(hour: number): string {
  return `${String(hour).padStart(2, '0')}:00`
}

export function pad2(value: number): string {
  return String(value).padStart(2, '0')
}
