import { useEffect, useRef } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { Icono } from '@/components/Icono'
import { Marca } from '@/components/Marca'

export default function RootLayout() {
  const { autenticado, perfil, salir, modoDemo, esAdmin } = useSesion()
  const { pathname } = useLocation()
  const main = useRef<HTMLElement>(null)
  useEffect(() => { main.current?.focus({ preventScroll: true }); window.scrollTo(0, 0) }, [pathname])
  return <div className={autenticado ? 'app-shell' : 'login-shell'}>
    <a href="#contenido" className="skip-link">Saltar al contenido</a>
    {autenticado && <aside className="sidebar">
      <Link to={esAdmin ? '/admin/grupos' : '/'} className="brand" aria-label="Fishy! Inicio"><Marca /></Link>
      <p className="nav-caption">ESPACIO DEL TUTOR</p>
      <nav aria-label="Navegación principal">
        {esAdmin
          ? <NavLink to="/admin/grupos" className={({ isActive }) => 'nav-link ' + (isActive ? 'active' : '')}><Icono nombre="grupo" />Mis grupos</NavLink>
          : <NavLink to="/" end className={() => !pathname.startsWith('/admin') ? 'nav-link active' : 'nav-link'}><Icono nombre="reportes" />Reportes</NavLink>}
      </nav>
      <div className="sidebar-bottom">
        <div className="privacy-side"><Icono nombre="escudo" /><strong>Un espacio de confianza</strong><p>Aprendizaje visible.<br />Privacidad protegida.</p></div>
        <div className="account"><span className="avatar small">{perfil?.nombre.slice(0, 1)}</span><div><strong>{perfil?.nombre}</strong><span>{esAdmin ? 'Tutor administrador' : 'Tutor padre/madre'}</span></div><button type="button" className="icon-button" onClick={salir} aria-label="Cerrar sesión" title="Cerrar sesión"><Icono nombre="salir" /></button></div>
      </div>
    </aside>}
    <div className="app-content">
      {autenticado && <header className="topbar"><span>Panel de tutores</span>{modoDemo && <span className="badge">Demostración · datos ficticios</span>}</header>}
      <main id="contenido" ref={main} tabIndex={-1} className={autenticado ? 'main-content' : 'login-main'}>
        <Outlet key={String(perfil?.id ?? 'guest') + ':' + modoDemo + ':' + esAdmin + ':' + pathname} />
      </main>
      {autenticado && <footer className="footer"><div className="footer-brand"><span className="brand"><Marca /></span><span>Aprender a navegar con seguridad</span></div><span>El progreso comienza con cada decisión.</span></footer>}
    </div>
  </div>
}
