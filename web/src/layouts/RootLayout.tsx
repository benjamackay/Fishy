import { Link, Outlet } from 'react-router-dom'

export default function RootLayout() {
  return (
    <div className="layout">
      <header className="layout__header">
        <Link to="/" className="layout__brand">
          Fishy!
        </Link>
      </header>

      <main className="layout__main">
        <Outlet />
      </main>
    </div>
  )
}
