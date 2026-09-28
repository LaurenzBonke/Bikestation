import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

// Deutsch ist die Referenz: Englisch und Niederländisch müssen genau dieselben Schlüssel haben
const de = {
  'app.title': 'Smart Bikestation',
  'app.titleStatistics': 'Statistik – Smart Bikestation',
  'app.titleAdmin': 'Admin – Smart Bikestation',
  'app.skip': 'Zum Inhalt springen',

  'nav.home': 'Smart Bikestation Startseite',
  'nav.main': 'Hauptnavigation',
  'nav.overview': 'Übersicht',
  'nav.statistics': 'Statistik',
  'nav.admin': 'Admin',
  'nav.connected': 'Live verbunden',
  'nav.disconnected': 'Keine Verbindung',
  'settings.language': 'Sprache',
  'settings.theme': 'Darstellung',
  'theme.system': 'System',
  'theme.light': 'Hell',
  'theme.dark': 'Dunkel',

  'dash.eyebrow': 'SMART BIKESTATION',
  'dash.title': 'Freien Stellplatz finden.',
  'dash.intro': 'Live-Belegung der Station – bevor du ankommst.',
  'dash.lastUpdate': 'Zuletzt aktualisiert:',
  'dash.noConnection': 'Keine Verbindung zum Server. Die angezeigten Daten sind eventuell veraltet.',
  'dash.stationAria': 'Belegung der Station',
  'dash.now': 'JETZT',
  'dash.available': 'Verfügbare Stellplätze',
  'dash.refresh': 'Aktualisierung alle 3 s',
  'dash.loading': 'Daten werden geladen …',
  'dash.of': 'von {total}',
  'dash.spotsFree': 'Plätzen frei',
  'dash.summary': '{free} frei · {taken} belegt',
  'dash.summaryOffline': ' · {offline} offline / ohne Daten',
  'dash.footer': 'Smart Bikestation – Schulprojekt-Prototyp',
  'dash.privacy': 'Keine Kameras, keine personenbezogenen Daten',

  'frame.label': 'STATION',
  'frame.free': 'GRÜN = FREI',
  'frame.taken': 'ROT = BELEGT',

  'slots.heading': 'STELLPLÄTZE',
  'slots.hint': 'Status und Sensorwerte je Platz',
  'slots.total': '{count} GESAMT',
  'slot.name': 'Stellplatz {id}',
  'status.free': 'Frei',
  'status.taken': 'Belegt',
  'status.offline': 'Offline',
  'status.unknown': 'Keine Daten',
  'slot.tamper': 'Mögliche Manipulation erkannt',
  'slot.anomaly': 'Ungewöhnliche Aktivität (KI-Erkennung)',
  'slot.offlineNote': 'Sensor meldet sich nicht – letzter Stand {time}. Status evtl. veraltet.',
  'slot.noData': 'Noch keine Sensordaten empfangen',
  'slot.values': 'Druck {pressure} · Abstand {distance} cm · Vibration {vibration} · {time}',
  'common.yes': 'ja',
  'common.no': 'nein',

  'alerts.kicker': 'MELDUNGEN',
  'alerts.heading': 'Offene Meldungen',
  'alerts.none': 'Keine offenen Meldungen. Alles in Ordnung.',
  'alerts.noneShort': 'Keine offenen Meldungen.',
  'alerts.footer': 'Meldungen entstehen durch Vibration oder die KI-Anomalieerkennung',
  'severity.Info': 'Hinweis',
  'severity.Warning': 'Warnung',
  'severity.Critical': 'Kritisch',
  'alertType.PossibleTampering': 'Vibration',
  'alertType.SensorMismatch': 'Sensoren widersprüchlich',
  'alertType.Anomaly': 'KI-Anomalie',
  'alert.tamper': 'Mögliche Manipulation an {slot} erkannt.',
  'alert.mismatch': 'Widersprüchliche Sensorwerte an {slot}.',
  'alert.anomaly': 'Ungewöhnliche Aktivität an {slot}.',
  'alert.anomalyReason': 'Ungewöhnliche Aktivität an {slot} ({reason}).',
  'reason.vibration': 'Vibration',
  'reason.pressure': 'ungewöhnlicher Druck',
  'reason.distance': 'ungewöhnlicher Abstand',
  'reason.pressureChange': 'starke Druckänderung',
  'reason.distanceChange': 'starke Abstandsänderung',
  'reason.combination': 'ungewöhnliche Kombination der Messwerte',

  'forecast.kicker': 'PROGNOSE',
  'forecast.heading': 'Die nächsten Stunden',
  'forecast.none': 'Noch zu wenig Daten für eine Prognose.',
  'forecast.free': 'ca. {free} von {total} frei',
  'forecast.note': 'Geschätzt aus der Belegung der letzten {days} Tage.',

  'stats.eyebrow': 'AUSWERTUNG',
  'stats.title': 'Statistik',
  'stats.intro': 'Auslastung und Ereignisse der Station.',
  'stats.period': 'Zeitraum',
  'stats.period1': '24 Stunden',
  'stats.period7': '7 Tage',
  'stats.period30': '30 Tage',
  'stats.error': 'Statistik konnte nicht geladen werden.',
  'stats.tiles': 'Kennzahlen',
  'stats.avg': 'Durchschnittliche Auslastung',
  'stats.peak': 'Stoßzeit',
  'stats.peakValue': '{hour} Uhr',
  'stats.tamper': 'Vibrations-Meldungen',
  'stats.anomalies': 'KI-Anomalien',
  'stats.byHourKicker': 'NACH UHRZEIT',
  'stats.byHour': 'Auslastung nach Uhrzeit (%)',
  'stats.showTable': 'Als Tabelle anzeigen',
  'stats.showChart': 'Als Diagramm anzeigen',
  'stats.empty': 'Noch keine Messwerte in diesem Zeitraum.',
  'stats.note': 'Anteil der Messungen, bei denen ein Platz belegt war · {count} Messungen',
  'stats.byDayKicker': 'VERLAUF',
  'stats.byDay': 'Auslastung pro Tag (%)',
  'stats.bySlotKicker': 'JE STELLPLATZ',
  'stats.bySlot': 'Stellplätze im Vergleich',
  'stats.colSlot': 'Stellplatz',
  'stats.colOccupancy': 'Auslastung',
  'stats.colTime': 'Uhrzeit',
  'stats.colDay': 'Tag',
  'stats.colReadings': 'Messungen',
  'stats.hourAria': '{hour} Uhr: {percent} Prozent belegt',
  'stats.dayAria': '{day}: {percent} Prozent belegt, {tamper} Vibrations-Meldungen, {anomalies} KI-Anomalien',
  'stats.tooltipHour': '{hour} Uhr',
  'stats.tooltipOccupied': '{percent} % belegt',
  'stats.tooltipReadings': '{count} Messungen',
  'stats.tooltipEvents': '{tamper} Vibration · {anomalies} KI-Anomalien',

  'login.eyebrow': 'GESCHÜTZTER BEREICH',
  'login.title': 'Admin-Anmeldung',
  'login.intro': 'Melde dich an, um Meldungen der Station zu bearbeiten.',
  'login.username': 'Benutzername',
  'login.password': 'Passwort',
  'login.submit': 'Anmelden',
  'login.submitting': 'Anmelden …',
  'login.invalid': 'Benutzername oder Passwort ist falsch.',
  'login.expired': 'Deine Sitzung ist abgelaufen. Bitte erneut anmelden.',
  'error.tooMany': 'Zu viele Versuche. Bitte eine Minute warten.',
  'error.generic': 'Das hat nicht geklappt. Bitte erneut versuchen.',

  'admin.eyebrow': 'STATIONSVERWALTUNG',
  'admin.hello': 'Hallo, {name}.',
  'admin.intro': 'Offene Meldungen prüfen und nach Kontrolle vor Ort als erledigt markieren.',
  'admin.loggedInUntil': 'Angemeldet bis',
  'admin.logout': 'Abmelden',
  'admin.alertsHeading': 'Offene Meldungen ({count})',
  'admin.resolve': 'Als erledigt markieren',
  'admin.resolving': 'Wird gespeichert …',
  'admin.resolved': 'Meldung an {slot} wurde als erledigt markiert.',
  'admin.demoKicker': 'PRÄSENTATION',
  'admin.demoHeading': 'Demo-Daten',
  'admin.demoText':
    'Erzeugt realistische Messwerte der letzten 7 Tage, damit Statistik und KI schon ohne Hardware etwas zeigen. Auf dem Server nur, wenn Demo:Enabled eingeschaltet ist.',
  'admin.demoButton': 'Demo-Daten erzeugen',
  'admin.demoGenerating': 'Wird erzeugt …',
  'admin.demoDone': '{count} Demo-Messwerte der letzten 7 Tage erzeugt.',
  'admin.demoDisabled': 'Demo-Daten sind auf diesem Server ausgeschaltet (Einstellung Demo:Enabled).',
}

export type TranslationKey = keyof typeof de
type Translations = Record<TranslationKey, string>

const en: Translations = {
  'app.title': 'Smart Bikestation',
  'app.titleStatistics': 'Statistics – Smart Bikestation',
  'app.titleAdmin': 'Admin – Smart Bikestation',
  'app.skip': 'Skip to content',

  'nav.home': 'Smart Bikestation home',
  'nav.main': 'Main navigation',
  'nav.overview': 'Overview',
  'nav.statistics': 'Statistics',
  'nav.admin': 'Admin',
  'nav.connected': 'Live connected',
  'nav.disconnected': 'No connection',
  'settings.language': 'Language',
  'settings.theme': 'Appearance',
  'theme.system': 'System',
  'theme.light': 'Light',
  'theme.dark': 'Dark',

  'dash.eyebrow': 'SMART BIKESTATION',
  'dash.title': 'Find a free spot.',
  'dash.intro': 'Live occupancy of the station – before you arrive.',
  'dash.lastUpdate': 'Last updated:',
  'dash.noConnection': 'No connection to the server. The data shown may be outdated.',
  'dash.stationAria': 'Station occupancy',
  'dash.now': 'RIGHT NOW',
  'dash.available': 'Available spots',
  'dash.refresh': 'Updates every 3 s',
  'dash.loading': 'Loading data …',
  'dash.of': 'of {total}',
  'dash.spotsFree': 'spots free',
  'dash.summary': '{free} free · {taken} occupied',
  'dash.summaryOffline': ' · {offline} offline / no data',
  'dash.footer': 'Smart Bikestation – school project prototype',
  'dash.privacy': 'No cameras, no personal data',

  'frame.label': 'STATION',
  'frame.free': 'GREEN = FREE',
  'frame.taken': 'RED = OCCUPIED',

  'slots.heading': 'PARKING SPOTS',
  'slots.hint': 'Status and sensor values per spot',
  'slots.total': '{count} TOTAL',
  'slot.name': 'Spot {id}',
  'status.free': 'Free',
  'status.taken': 'Occupied',
  'status.offline': 'Offline',
  'status.unknown': 'No data',
  'slot.tamper': 'Possible tampering detected',
  'slot.anomaly': 'Unusual activity (AI detection)',
  'slot.offlineNote': 'Sensor not reporting – last update {time}. Status may be outdated.',
  'slot.noData': 'No sensor data received yet',
  'slot.values': 'Pressure {pressure} · Distance {distance} cm · Vibration {vibration} · {time}',
  'common.yes': 'yes',
  'common.no': 'no',

  'alerts.kicker': 'ALERTS',
  'alerts.heading': 'Open alerts',
  'alerts.none': 'No open alerts. All good.',
  'alerts.noneShort': 'No open alerts.',
  'alerts.footer': 'Alerts are raised by vibration or the AI anomaly detection',
  'severity.Info': 'Info',
  'severity.Warning': 'Warning',
  'severity.Critical': 'Critical',
  'alertType.PossibleTampering': 'Vibration',
  'alertType.SensorMismatch': 'Sensors disagree',
  'alertType.Anomaly': 'AI anomaly',
  'alert.tamper': 'Possible tampering detected at {slot}.',
  'alert.mismatch': 'Conflicting sensor values at {slot}.',
  'alert.anomaly': 'Unusual activity at {slot}.',
  'alert.anomalyReason': 'Unusual activity at {slot} ({reason}).',
  'reason.vibration': 'vibration',
  'reason.pressure': 'unusual pressure',
  'reason.distance': 'unusual distance',
  'reason.pressureChange': 'sharp pressure change',
  'reason.distanceChange': 'sharp distance change',
  'reason.combination': 'unusual combination of readings',

  'forecast.kicker': 'FORECAST',
  'forecast.heading': 'The next hours',
  'forecast.none': 'Not enough data for a forecast yet.',
  'forecast.free': 'approx. {free} of {total} free',
  'forecast.note': 'Estimated from the occupancy of the last {days} days.',

  'stats.eyebrow': 'ANALYSIS',
  'stats.title': 'Statistics',
  'stats.intro': 'Occupancy and events of the station.',
  'stats.period': 'Period',
  'stats.period1': '24 hours',
  'stats.period7': '7 days',
  'stats.period30': '30 days',
  'stats.error': 'Statistics could not be loaded.',
  'stats.tiles': 'Key figures',
  'stats.avg': 'Average occupancy',
  'stats.peak': 'Peak time',
  'stats.peakValue': '{hour}',
  'stats.tamper': 'Vibration alerts',
  'stats.anomalies': 'AI anomalies',
  'stats.byHourKicker': 'BY TIME OF DAY',
  'stats.byHour': 'Occupancy by time of day (%)',
  'stats.showTable': 'Show as table',
  'stats.showChart': 'Show as chart',
  'stats.empty': 'No readings in this period yet.',
  'stats.note': 'Share of readings in which a spot was occupied · {count} readings',
  'stats.byDayKicker': 'HISTORY',
  'stats.byDay': 'Occupancy per day (%)',
  'stats.bySlotKicker': 'PER SPOT',
  'stats.bySlot': 'Spots compared',
  'stats.colSlot': 'Spot',
  'stats.colOccupancy': 'Occupancy',
  'stats.colTime': 'Time',
  'stats.colDay': 'Day',
  'stats.colReadings': 'Readings',
  'stats.hourAria': '{hour}: {percent} percent occupied',
  'stats.dayAria': '{day}: {percent} percent occupied, {tamper} vibration alerts, {anomalies} AI anomalies',
  'stats.tooltipHour': '{hour}',
  'stats.tooltipOccupied': '{percent} % occupied',
  'stats.tooltipReadings': '{count} readings',
  'stats.tooltipEvents': '{tamper} vibration · {anomalies} AI anomalies',

  'login.eyebrow': 'RESTRICTED AREA',
  'login.title': 'Admin sign-in',
  'login.intro': 'Sign in to manage the station alerts.',
  'login.username': 'Username',
  'login.password': 'Password',
  'login.submit': 'Sign in',
  'login.submitting': 'Signing in …',
  'login.invalid': 'Username or password is incorrect.',
  'login.expired': 'Your session has expired. Please sign in again.',
  'error.tooMany': 'Too many attempts. Please wait a minute.',
  'error.generic': 'Something went wrong. Please try again.',

  'admin.eyebrow': 'STATION MANAGEMENT',
  'admin.hello': 'Hello, {name}.',
  'admin.intro': 'Review open alerts and mark them as resolved after checking on site.',
  'admin.loggedInUntil': 'Signed in until',
  'admin.logout': 'Sign out',
  'admin.alertsHeading': 'Open alerts ({count})',
  'admin.resolve': 'Mark as resolved',
  'admin.resolving': 'Saving …',
  'admin.resolved': 'Alert at {slot} was marked as resolved.',
  'admin.demoKicker': 'PRESENTATION',
  'admin.demoHeading': 'Demo data',
  'admin.demoText':
    'Creates realistic readings for the last 7 days so statistics and AI have something to show without hardware. On the server only when Demo:Enabled is switched on.',
  'admin.demoButton': 'Create demo data',
  'admin.demoGenerating': 'Creating …',
  'admin.demoDone': '{count} demo readings for the last 7 days created.',
  'admin.demoDisabled': 'Demo data is switched off on this server (setting Demo:Enabled).',
}

const nl: Translations = {
  'app.title': 'Smart Bikestation',
  'app.titleStatistics': 'Statistieken – Smart Bikestation',
  'app.titleAdmin': 'Beheer – Smart Bikestation',
  'app.skip': 'Naar de inhoud',

  'nav.home': 'Smart Bikestation startpagina',
  'nav.main': 'Hoofdnavigatie',
  'nav.overview': 'Overzicht',
  'nav.statistics': 'Statistieken',
  'nav.admin': 'Beheer',
  'nav.connected': 'Live verbonden',
  'nav.disconnected': 'Geen verbinding',
  'settings.language': 'Taal',
  'settings.theme': 'Weergave',
  'theme.system': 'Systeem',
  'theme.light': 'Licht',
  'theme.dark': 'Donker',

  'dash.eyebrow': 'SMART BIKESTATION',
  'dash.title': 'Vind een vrije plek.',
  'dash.intro': 'Live bezetting van het station – voordat je aankomt.',
  'dash.lastUpdate': 'Laatst bijgewerkt:',
  'dash.noConnection': 'Geen verbinding met de server. De getoonde gegevens zijn mogelijk verouderd.',
  'dash.stationAria': 'Bezetting van het station',
  'dash.now': 'NU',
  'dash.available': 'Beschikbare plekken',
  'dash.refresh': 'Elke 3 s bijgewerkt',
  'dash.loading': 'Gegevens worden geladen …',
  'dash.of': 'van {total}',
  'dash.spotsFree': 'plekken vrij',
  'dash.summary': '{free} vrij · {taken} bezet',
  'dash.summaryOffline': ' · {offline} offline / geen gegevens',
  'dash.footer': 'Smart Bikestation – prototype schoolproject',
  'dash.privacy': "Geen camera's, geen persoonsgegevens",

  'frame.label': 'STATION',
  'frame.free': 'GROEN = VRIJ',
  'frame.taken': 'ROOD = BEZET',

  'slots.heading': 'FIETSPLEKKEN',
  'slots.hint': 'Status en sensorwaarden per plek',
  'slots.total': '{count} TOTAAL',
  'slot.name': 'Plek {id}',
  'status.free': 'Vrij',
  'status.taken': 'Bezet',
  'status.offline': 'Offline',
  'status.unknown': 'Geen gegevens',
  'slot.tamper': 'Mogelijke manipulatie gedetecteerd',
  'slot.anomaly': 'Ongebruikelijke activiteit (AI-detectie)',
  'slot.offlineNote': 'Sensor meldt zich niet – laatste stand {time}. Status mogelijk verouderd.',
  'slot.noData': 'Nog geen sensorgegevens ontvangen',
  'slot.values': 'Druk {pressure} · Afstand {distance} cm · Trilling {vibration} · {time}',
  'common.yes': 'ja',
  'common.no': 'nee',

  'alerts.kicker': 'MELDINGEN',
  'alerts.heading': 'Open meldingen',
  'alerts.none': 'Geen open meldingen. Alles in orde.',
  'alerts.noneShort': 'Geen open meldingen.',
  'alerts.footer': 'Meldingen ontstaan door trillingen of de AI-anomaliedetectie',
  'severity.Info': 'Info',
  'severity.Warning': 'Waarschuwing',
  'severity.Critical': 'Kritiek',
  'alertType.PossibleTampering': 'Trilling',
  'alertType.SensorMismatch': 'Sensoren tegenstrijdig',
  'alertType.Anomaly': 'AI-anomalie',
  'alert.tamper': 'Mogelijke manipulatie bij {slot} gedetecteerd.',
  'alert.mismatch': 'Tegenstrijdige sensorwaarden bij {slot}.',
  'alert.anomaly': 'Ongebruikelijke activiteit bij {slot}.',
  'alert.anomalyReason': 'Ongebruikelijke activiteit bij {slot} ({reason}).',
  'reason.vibration': 'trilling',
  'reason.pressure': 'ongebruikelijke druk',
  'reason.distance': 'ongebruikelijke afstand',
  'reason.pressureChange': 'sterke drukverandering',
  'reason.distanceChange': 'sterke afstandsverandering',
  'reason.combination': 'ongebruikelijke combinatie van meetwaarden',

  'forecast.kicker': 'VOORSPELLING',
  'forecast.heading': 'De komende uren',
  'forecast.none': 'Nog te weinig gegevens voor een voorspelling.',
  'forecast.free': 'ca. {free} van {total} vrij',
  'forecast.note': 'Geschat op basis van de bezetting van de afgelopen {days} dagen.',

  'stats.eyebrow': 'ANALYSE',
  'stats.title': 'Statistieken',
  'stats.intro': 'Bezetting en gebeurtenissen van het station.',
  'stats.period': 'Periode',
  'stats.period1': '24 uur',
  'stats.period7': '7 dagen',
  'stats.period30': '30 dagen',
  'stats.error': 'Statistieken konden niet worden geladen.',
  'stats.tiles': 'Kerncijfers',
  'stats.avg': 'Gemiddelde bezetting',
  'stats.peak': 'Piekuur',
  'stats.peakValue': '{hour} uur',
  'stats.tamper': 'Trillingsmeldingen',
  'stats.anomalies': 'AI-anomalieën',
  'stats.byHourKicker': 'PER TIJDSTIP',
  'stats.byHour': 'Bezetting per tijdstip (%)',
  'stats.showTable': 'Als tabel tonen',
  'stats.showChart': 'Als grafiek tonen',
  'stats.empty': 'Nog geen meetwaarden in deze periode.',
  'stats.note': 'Aandeel metingen waarbij een plek bezet was · {count} metingen',
  'stats.byDayKicker': 'VERLOOP',
  'stats.byDay': 'Bezetting per dag (%)',
  'stats.bySlotKicker': 'PER PLEK',
  'stats.bySlot': 'Plekken vergeleken',
  'stats.colSlot': 'Plek',
  'stats.colOccupancy': 'Bezetting',
  'stats.colTime': 'Tijdstip',
  'stats.colDay': 'Dag',
  'stats.colReadings': 'Metingen',
  'stats.hourAria': '{hour} uur: {percent} procent bezet',
  'stats.dayAria': '{day}: {percent} procent bezet, {tamper} trillingsmeldingen, {anomalies} AI-anomalieën',
  'stats.tooltipHour': '{hour} uur',
  'stats.tooltipOccupied': '{percent} % bezet',
  'stats.tooltipReadings': '{count} metingen',
  'stats.tooltipEvents': '{tamper} trilling · {anomalies} AI-anomalieën',

  'login.eyebrow': 'BEVEILIGD GEDEELTE',
  'login.title': 'Beheerder aanmelden',
  'login.intro': 'Meld je aan om de meldingen van het station te beheren.',
  'login.username': 'Gebruikersnaam',
  'login.password': 'Wachtwoord',
  'login.submit': 'Aanmelden',
  'login.submitting': 'Aanmelden …',
  'login.invalid': 'Gebruikersnaam of wachtwoord is onjuist.',
  'login.expired': 'Je sessie is verlopen. Meld je opnieuw aan.',
  'error.tooMany': 'Te veel pogingen. Wacht een minuut.',
  'error.generic': 'Dat is niet gelukt. Probeer het opnieuw.',

  'admin.eyebrow': 'STATIONSBEHEER',
  'admin.hello': 'Hallo, {name}.',
  'admin.intro': 'Controleer open meldingen en markeer ze als afgehandeld na controle ter plaatse.',
  'admin.loggedInUntil': 'Aangemeld tot',
  'admin.logout': 'Afmelden',
  'admin.alertsHeading': 'Open meldingen ({count})',
  'admin.resolve': 'Als afgehandeld markeren',
  'admin.resolving': 'Wordt opgeslagen …',
  'admin.resolved': 'Melding bij {slot} is als afgehandeld gemarkeerd.',
  'admin.demoKicker': 'PRESENTATIE',
  'admin.demoHeading': 'Demogegevens',
  'admin.demoText':
    'Maakt realistische meetwaarden van de afgelopen 7 dagen aan, zodat statistieken en AI al zonder hardware iets laten zien. Op de server alleen als Demo:Enabled is ingeschakeld.',
  'admin.demoButton': 'Demogegevens aanmaken',
  'admin.demoGenerating': 'Wordt aangemaakt …',
  'admin.demoDone': '{count} demo-meetwaarden van de afgelopen 7 dagen aangemaakt.',
  'admin.demoDisabled': 'Demogegevens zijn op deze server uitgeschakeld (instelling Demo:Enabled).',
}

export type Language = 'de' | 'en' | 'nl'

export const LANGUAGES: { code: Language; label: string }[] = [
  { code: 'de', label: 'Deutsch' },
  { code: 'en', label: 'English' },
  { code: 'nl', label: 'Nederlands' },
]

const translations: Record<Language, Translations> = { de, en, nl }
const locales: Record<Language, string> = { de: 'de-DE', en: 'en-GB', nl: 'nl-NL' }
const STORAGE_KEY = 'bikestation-language'

export type TranslateParams = Record<string, string | number>

type I18n = {
  language: Language
  locale: string
  setLanguage: (language: Language) => void
  t: (key: TranslationKey, params?: TranslateParams) => string
}

const I18nContext = createContext<I18n | null>(null)

function initialLanguage(): Language {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved === 'de' || saved === 'en' || saved === 'nl') return saved
  } catch {
    // Speicher nicht verfügbar – Browsersprache verwenden
  }
  const browser = navigator.language.toLowerCase()
  if (browser.startsWith('nl')) return 'nl'
  if (browser.startsWith('en')) return 'en'
  return 'de'
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(initialLanguage)

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next)
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // Auswahl gilt dann nur bis zum Neuladen
    }
  }, [])

  // Screenreader und Silbentrennung brauchen die richtige Sprache am <html>-Element
  useEffect(() => {
    document.documentElement.lang = language
  }, [language])

  const value = useMemo<I18n>(() => {
    const dictionary = translations[language]
    return {
      language,
      locale: locales[language],
      setLanguage,
      t: (key, params) =>
        dictionary[key].replace(/\{(\w+)\}/g, (match, name: string) =>
          params && name in params ? String(params[name]) : match,
        ),
    }
  }, [language, setLanguage])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n(): I18n {
  const context = useContext(I18nContext)
  if (!context) throw new Error('useI18n muss innerhalb von I18nProvider verwendet werden')
  return context
}
