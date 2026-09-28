import { useEffect, type MouseEvent } from 'react'
import Header from './components/Header'
import Dashboard from './components/Dashboard'
import AdminPage from './components/AdminPage'
import { useStationData } from './useStationData'
import { useAuth } from './useAuth'
import { useRoute } from './useRoute'

function skipToContent(event: MouseEvent<HTMLAnchorElement>) {
  event.preventDefault()
  document.getElementById('main')?.focus()
}

export default function App() {
  const data = useStationData()
  const auth = useAuth()
  const route = useRoute()

  useEffect(() => {
    document.title = route === 'admin' ? 'Admin – Smart Bikestation' : 'Smart Bikestation'
  }, [route])

  return (
    <>
      {/* Klick wird abgefangen, weil "#main" sonst über das Hash-Routing die Seite wechseln würde */}
      <a className="skip-link" href="#main" onClick={skipToContent}>
        Zum Inhalt springen
      </a>
      <Header connected={data.connected} route={route} />
      {route === 'admin' ? <AdminPage auth={auth} data={data} /> : <Dashboard data={data} />}
    </>
  )
}
