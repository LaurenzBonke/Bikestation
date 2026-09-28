const timeFormat = new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
const dateTimeFormat = new Intl.DateTimeFormat('de-DE', {
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})

export function formatTime(value: string | Date | null): string {
  if (!value) return '–'
  return timeFormat.format(new Date(value))
}

export function formatDateTime(value: string): string {
  return dateTimeFormat.format(new Date(value))
}

export function pad2(value: number): string {
  return String(value).padStart(2, '0')
}
