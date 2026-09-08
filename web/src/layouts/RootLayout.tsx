import { Link, NavLink, Outlet } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'

function claseNav({ isActive }: { isActive: boolean }) {
  return isActive ? 'activo' : undefined
}

export default function RootLayout() {
  const { autenticado, esAdmin, perfil, salir } = useSesion()

  return (
    <div className="layout">
      <header className="layout__header">
        <Link to="/" className="layout__brand">
          Fishy!
        </Link>

        {autenticado && (
          <nav className="layout__nav">
            <NavLink to="/" end className={claseNav}>
              Tus sesiones
            </NavLink>
            {esAdmin && (
              <NavLink to="/admin/grupos" className={claseNav}>
                Grupos
              </NavLink>
            )}
          </nav>
        )}

        <div className="layout__cuenta">
          {autenticado ? (
            <>
              <span>{perfil?.nombre}</span>
              <button type="button" className="boton" onClick={salir}>
                Salir
              </button>
            </>
          ) : (
            <Link to="/login">Entrar</Link>
          )}
        </div>
      </header>

      <main className="layout__main">
        <Outlet />
      </main>
    </div>
  )
}
