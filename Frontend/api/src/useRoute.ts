import { useEffect, useState } from 'react'

export type Route = 'overview' | 'admin'

function currentRoute(): Route {
  return window.location.hash.startsWith('#admin') ? 'admin' : 'overview'
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
