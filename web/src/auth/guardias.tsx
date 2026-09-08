import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Cargando } from '@/components/Cargando'
import { useSesion } from './contexto'

/** Deja pasar solo con sesion iniciada; si no, manda al login. */
export function RequiereSesion() {
  const { autenticado, cargando } = useSesion()
  const ubicacion = useLocation()

  if (cargando) return <Cargando mensaje="Recuperando tu sesion..." />

  if (!autenticado) {
    // Se guarda de donde venia para volver ahi despues del login.
    return <Navigate to="/login" replace state={{ desde: ubicacion.pathname }} />
  }

  return <Outlet />
}

/** Ademas de sesion, exige que la cuenta pueda administrar grupos. */
export function RequiereAdmin() {
  const { esAdmin, cargando } = useSesion()

  if (cargando) return <Cargando mensaje="Comprobando permisos..." />

  if (!esAdmin) {
    return (
      <section className="card">
        <h1>No tienes acceso a esta seccion</h1>
        <p className="muted">
          El panel de administracion es solo para cuentas marcadas como
          administrador (<code>is_admin</code>).
        </p>
        <p className="muted">
          Hoy el backend no expone ese campo en <code>/auth/perfil/</code>, asi
          que ninguna cuenta lo tiene desde el frontend. Para trabajar el panel
          mientras tanto, pon <code>VITE_FORZAR_ADMIN=true</code> en tu{' '}
          <code>.env</code> y reinicia el servidor de desarrollo.
        </p>
      </section>
    )
  }

  return <Outlet />
}
