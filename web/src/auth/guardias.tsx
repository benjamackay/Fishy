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
  const { esProfesor, cargando, perfil, salir } = useSesion()
  if (cargando) return <Cargando mensaje="Comprobando permisos…" />
  if (esProfesor) return <Navigate to="/admin/grupos" replace />
  if (perfil?.rol !== 'padre') return <section className="card">
    <h1>{perfil?.rol === 'admin' ? 'Cuenta administradora de Fishy' : 'Acceso no disponible'}</h1>
    <p>Esta cuenta no tiene reportes individuales. Inicia sesión con una cuenta de padre o madre para consultarlos, o de profesor para gestionar sus grupos.</p>
    <button type="button" className="boton" onClick={salir}>Cambiar de cuenta</button>
  </section>
  return <Outlet />
}

/** Protege todo el árbol /admin, incluidos detalle, reporte y enlaces directos. */
export function RequiereAdmin() {
  const { esProfesor, cargando } = useSesion()
  if (cargando) return <Cargando mensaje="Comprobando permisos…" />
  if (!esProfesor) return <section className="card" role="alert">
    <h1>Acceso exclusivo para profesores</h1>
    <p className="muted">Necesitas una cuenta de profesor para gestionar grupos.</p>
    <Link className="boton" to="/">Volver al inicio</Link>
  </section>
  return <Outlet />
}
