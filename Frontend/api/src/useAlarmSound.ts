import { useEffect, useRef, useState } from 'react'

// Alarmton (drei klare Töne, alle paar Sekunden) – deutlich hörbar, aber kein schrilles Piepen.
// Läuft, solange `active` wahr ist; bestätigt der Nutzer die Meldung, verstummt er.
// Zusätzlich: Vibration (Android), blinkender Tab-Titel und – wenn erlaubt – eine System-Benachrichtigung.
const REPEAT_MS = 4000
const VOLUME = 0.45
const NOTIFY_TAG = 'bikestation-alarm'

type AudioContextClass = typeof AudioContext

function playChime(context: AudioContext) {
  const start = context.currentTime + 0.02
  // Drei aufsteigende Töne mit weichem Ein- und Ausklang
  ;[
    { freq: 660, at: 0 },
    { freq: 880, at: 0.3 },
    { freq: 1100, at: 0.6 },
  ].forEach(({ freq, at }) => {
    const osc = context.createOscillator()
    const gain = context.createGain()
    osc.type = 'triangle'
    osc.frequency.value = freq
    const t0 = start + at
    gain.gain.setValueAtTime(0, t0)
    gain.gain.linearRampToValueAtTime(VOLUME, t0 + 0.04)
    gain.gain.exponentialRampToValueAtTime(0.0001, t0 + 0.45)
    osc.connect(gain).connect(context.destination)
    osc.start(t0)
    osc.stop(t0 + 0.5)
  })
}

// System-Benachrichtigung – nur möglich über HTTPS (oder localhost) und wenn der Nutzer sie erlaubt hat
export function notificationsSupported(): boolean {
  return typeof window !== 'undefined' && window.isSecureContext && 'Notification' in window
}

async function showNotification(title: string, body: string) {
  if (!notificationsSupported() || Notification.permission !== 'granted') return
  const options: NotificationOptions = { body, tag: NOTIFY_TAG, requireInteraction: true }
  try {
    const registration = await navigator.serviceWorker?.getRegistration()
    if (registration) await registration.showNotification(title, options)
    else new Notification(title, options)
  } catch {
    // Manche Handy-Browser erlauben "new Notification" nicht – dann eben nur Ton und Banner
  }
}

// needsGesture: Browser blockiert Ton, bis man einmal auf die Seite geklickt hat
export function useAlarmSound(
  active: boolean,
  message?: { title: string; body: string },
): { needsGesture: boolean; enable: () => void } {
  const contextRef = useRef<AudioContext | null>(null)
  const [needsGesture, setNeedsGesture] = useState(false)

  function context(): AudioContext | null {
    if (contextRef.current) return contextRef.current
    const Ctor: AudioContextClass | undefined =
      window.AudioContext ?? (window as unknown as { webkitAudioContext?: AudioContextClass }).webkitAudioContext
    if (!Ctor) return null
    contextRef.current = new Ctor()
    return contextRef.current
  }

  function enable() {
    const ctx = context()
    if (!ctx) return
    void ctx.resume().then(() => {
      setNeedsGesture(false)
      playChime(ctx)
    })
  }

  useEffect(() => {
    if (!active) {
      setNeedsGesture(false)
      return
    }
    const ctx = context()
    if (!ctx) return

    const ring = () => {
      if (ctx.state === 'running') playChime(ctx)
      else setNeedsGesture(true)
      navigator.vibrate?.([400, 150, 400])
    }

    // Tab-Titel blinkt, damit man den Alarm auch in einem anderen Tab bemerkt
    const originalTitle = document.title
    let flash = false
    const titleTimer = window.setInterval(() => {
      flash = !flash
      document.title = flash && message ? `⚠ ${message.title}` : originalTitle
    }, 1000)
    if (message) void showNotification(message.title, message.body)
    void ctx.resume().finally(ring)
    const timer = window.setInterval(ring, REPEAT_MS)

    // Erster Klick/Tastendruck irgendwo auf der Seite schaltet den Ton frei
    const unlock = () => {
      void ctx.resume().then(() => setNeedsGesture(false))
    }
    window.addEventListener('pointerdown', unlock)
    window.addEventListener('keydown', unlock)
    return () => {
      window.clearInterval(timer)
      window.clearInterval(titleTimer)
      document.title = originalTitle
      navigator.vibrate?.(0)
      window.removeEventListener('pointerdown', unlock)
      window.removeEventListener('keydown', unlock)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- Meldungstext ändert den Alarm nicht
  }, [active])

  useEffect(() => () => void contextRef.current?.close(), [])

  return { needsGesture, enable }
}
