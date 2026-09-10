import { Link, Navigate, Outlet, useLocation } from 'react-router-dom'
import { Cargando } from '@/components/Cargando'
import { useSesion } from './contexto'

export function RequiereSesion() {
  const { autenticado, cargando } = useSesion()
  const ubicacion = useLocation()
  if (cargando) return <Cargando mensaje="Recuperando tu sesión…" />
  if (!autenticado) return <Navigate to="/login" replace state={{ desde: ubicacion.pathname }} />
  return <Outlet />
}

/** Los profesores no tienen perfiles infantiles asociados ni vistas individuales. */
export function RequierePadre() {
  const { esAdmin, cargando } = useSesion()
  if (cargando) return <Cargando mensaje="Comprobando permisos…" />
  if (esAdmin) return <Navigate to="/admin/grupos" replace />
  return <Outlet />
}

/** Protege todo el árbol /admin, incluidos detalle, reporte y enlaces directos. */
export function RequiereAdmin() {
  const { esAdmin, cargando } = useSesion()
  if (cargando) return <Cargando mensaje="Comprobando permisos…" />
  if (!esAdmin) return <section className="card" role="alert">
    <h1>Acceso exclusivo para profesores</h1>
    <p className="muted">La creación y gestión de grupos está disponible para tutores administradores. Como padre o madre, puedes consultar los reportes de tus hijos.</p>
    <Link className="boton" to="/">Ver los reportes de mis hijos</Link>
  </section>
  return <Outlet />
}
