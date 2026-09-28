import { useEffect, useState } from 'react'

export type Route = 'overview' | 'boxes' | 'account' | 'statistics' | 'admin'

function currentRoute(): Route {
  const hash = window.location.hash
  if (hash.startsWith('#admin')) return 'admin'
  if (hash.startsWith('#statistik')) return 'statistics'
  if (hash.startsWith('#boxen')) return 'boxes'
  if (hash.startsWith('#konto')) return 'account'
  return 'overview'
}

// Einfaches Hash-Routing (#admin), damit wir keine extra Router-Bibliothek brauchen
export function useRoute(): Route {
  const [route, setRoute] = useState<Route>(currentRoute)

  useEffect(() => {
    const onChange = () => setRoute(currentRoute())
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])

  return route
}
