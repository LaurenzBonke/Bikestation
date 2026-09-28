import Header from './components/Header'
import Dashboard from './components/Dashboard'
import { useStationData } from './useStationData'

export default function App() {
  const data = useStationData()

  return (
    <>
      <a className="skip-link" href="#main">
        Zum Inhalt springen
      </a>
      <Header connected={data.connected} />
      <Dashboard data={data} />
    </>
  )
}
