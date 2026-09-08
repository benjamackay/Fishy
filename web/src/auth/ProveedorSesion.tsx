import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { login, logout, obtenerPerfil } from '@/api/auth'
import { FORZAR_ADMIN } from '@/lib/config'
import { getToken } from '@/lib/token'
import { ContextoSesion } from './contexto'
import type { Sesion } from './contexto'
import type { AdultoResponsable } from '@/types/api'

/**
 * Mantiene la sesion del adulto responsable. Al montar intenta recuperar el
 * perfil con el token guardado; si el backend lo rechaza (401) se limpia, que
 * es el caso de un token vencido o de una BD reseteada.
 */
export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [perfil, setPerfil] = useState<AdultoResponsable | null>(null)
  // Sin token no hay nada que recuperar: se arranca ya resuelto, en vez de
  // apagar el "cargando" desde dentro del efecto.
  const [cargando, setCargando] = useState(() => getToken() !== null)

  useEffect(() => {
    if (!getToken()) return

    let vigente = true

    obtenerPerfil()
      .then((datos) => {
        if (vigente) setPerfil(datos)
      })
      .catch(() => {
        logout()
        if (vigente) setPerfil(null)
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [])

  const entrar = useCallback(async (nombre: string, password: string) => {
    await login(nombre, password)
    setPerfil(await obtenerPerfil())
  }, [])

  const salir = useCallback(() => {
    logout()
    setPerfil(null)
  }, [])

  const valor = useMemo<Sesion>(
    () => ({
      perfil,
      cargando,
      autenticado: perfil !== null,
      // `is_admin` todavia no viaja en la respuesta del backend; sin el flag de
      // desarrollo ninguna cuenta se ve como admin.
      esAdmin: perfil !== null && (perfil.is_admin === true || FORZAR_ADMIN),
      entrar,
      salir,
    }),
    [perfil, cargando, entrar, salir],
  )

  return (
    <ContextoSesion.Provider value={valor}>{children}</ContextoSesion.Provider>
  )
}
