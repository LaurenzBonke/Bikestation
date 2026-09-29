import { useEffect, useRef, useState } from 'react'

// Sanfter Alarmton (zwei weiche Töne, alle paar Sekunden) – bewusst nicht schrill.
// Läuft, solange `active` wahr ist; bestätigt der Nutzer die Meldung, verstummt er.
const REPEAT_MS = 5000
const VOLUME = 0.12

type AudioContextClass = typeof AudioContext

function playChime(context: AudioContext) {
  const start = context.currentTime + 0.02
  // Zwei aufsteigende Töne mit weichem Ein- und Ausklang
  ;[
    { freq: 660, at: 0 },
    { freq: 880, at: 0.32 },
  ].forEach(({ freq, at }) => {
    const osc = context.createOscillator()
    const gain = context.createGain()
    osc.type = 'sine'
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

// needsGesture: Browser blockiert Ton, bis man einmal auf die Seite geklickt hat
export function useAlarmSound(active: boolean): { needsGesture: boolean; enable: () => void } {
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
    }
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
      window.removeEventListener('pointerdown', unlock)
      window.removeEventListener('keydown', unlock)
    }
  }, [active])

  useEffect(() => () => void contextRef.current?.close(), [])

  return { needsGesture, enable }
}
