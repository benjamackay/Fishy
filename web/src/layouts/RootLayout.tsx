import { useEffect, useRef } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { Icono } from '@/components/Icono'
import { Marca } from '@/components/Marca'
import './header.css'

export default function RootLayout() {
  const { autenticado, perfil, salir, esProfesor } = useSesion()
  const { pathname } = useLocation()
  const main = useRef<HTMLElement>(null)
  useEffect(() => { main.current?.focus({ preventScroll: true }); window.scrollTo(0, 0) }, [pathname])
  return <div className={autenticado ? 'app-shell' : 'login-shell'}>
    <a href="#contenido" className="skip-link">Saltar al contenido</a>
    {autenticado && <header className="app-header"><div className="header-inner">
      <Link to={esProfesor ? '/admin/grupos' : '/'} className="brand" aria-label="Fishy! Inicio"><Marca /></Link>
      <nav className="header-nav" aria-label="Navegación principal">
        {esProfesor
          ? <NavLink to="/admin/grupos" className={({ isActive }) => 'nav-link ' + (isActive ? 'active' : '')}><Icono nombre="grupo" />Mis grupos</NavLink>
          : perfil?.rol === 'padre' ? <NavLink to="/" end className={() => !pathname.startsWith('/admin') ? 'nav-link active' : 'nav-link'}><Icono nombre="reportes" />Reportes</NavLink> : null}
      </nav>
        <div className="account"><span className="avatar small">{perfil?.nombre.slice(0, 1)}</span><div><strong>{perfil?.nombre}</strong><span>{esProfesor ? 'Tutor administrador' : perfil?.rol === 'padre' ? 'Tutor padre/madre' : perfil?.rol === 'admin' ? 'Administrador de Fishy' : 'Cuenta sin rol asignado'}</span></div><button type="button" className="icon-button" onClick={salir} aria-label="Cerrar sesión" title="Cerrar sesión"><Icono nombre="salir" /></button></div>
    </div></header>}
    <div className="app-content">
      <main id="contenido" ref={main} tabIndex={-1} className={autenticado ? 'main-content' : 'login-main'}>
        <Outlet key={String(perfil?.id ?? 'guest') + ':' + esProfesor + ':' + pathname} />
      </main>
    </div>
  </div>
}
