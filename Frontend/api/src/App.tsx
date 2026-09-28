import { useEffect, type MouseEvent } from 'react'
import Header from './components/Header'
import Dashboard from './components/Dashboard'
import AdminPage from './components/AdminPage'
import StatisticsPage from './components/StatisticsPage'
import { useStationData } from './useStationData'
import { useAuth } from './useAuth'
import { useRoute } from './useRoute'
import { useI18n } from './i18n'

function skipToContent(event: MouseEvent<HTMLAnchorElement>) {
  event.preventDefault()
  document.getElementById('main')?.focus()
}

export default function App() {
  const data = useStationData()
  const auth = useAuth()
  const route = useRoute()
  const { t } = useI18n()

  useEffect(() => {
    const titles = { overview: t('app.title'), statistics: t('app.titleStatistics'), admin: t('app.titleAdmin') }
    document.title = titles[route]
  }, [route, t])

  return (
    <>
      {/* Klick wird abgefangen, weil "#main" sonst über das Hash-Routing die Seite wechseln würde */}
      <a className="skip-link" href="#main" onClick={skipToContent}>
        {t('app.skip')}
      </a>
      <Header connected={data.connected} route={route} />
      {route === 'admin' && <AdminPage auth={auth} data={data} />}
      {route === 'statistics' && <StatisticsPage />}
      {route === 'overview' && <Dashboard data={data} />}
    </>
  )
}
