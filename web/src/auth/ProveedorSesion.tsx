import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { login, logout, obtenerPerfil } from '@/api/auth'
import { getToken } from '@/lib/token'
import { ContextoSesion } from './contexto'
import type { Sesion } from './contexto'
import type { AdultoResponsable } from '@/types/api'

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const generacion = useRef(0)
  const [perfil, setPerfil] = useState<AdultoResponsable | null>(null)
  const [cargando, setCargando] = useState(() => getToken() !== null)
  useEffect(() => {
    if (!getToken()) return
    const actual = generacion.current
    let vigente = true
    obtenerPerfil().then(datos => {
      if (vigente && actual === generacion.current) setPerfil(datos)
    }).catch(() => {
      if (vigente && actual === generacion.current) { logout(); setPerfil(null) }
    }).finally(() => { if (vigente && actual === generacion.current) setCargando(false) })
    return () => { vigente = false }
    // Recuperación inicial; las entradas posteriores las resuelven entrar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  const entrar = useCallback(async (nombre: string, password: string) => {
    const actual = ++generacion.current
    logout()
    try {
      await login(nombre, password)
      const datos = await obtenerPerfil()
      if (actual !== generacion.current) return
      setPerfil(datos)
    } catch (error) { if (actual === generacion.current) logout(); throw error }
    finally { if (actual === generacion.current) setCargando(false) }
  }, [])
  const salir = useCallback(() => {
    generacion.current += 1
    logout()
    setPerfil(null)
  }, [])
  useEffect(() => {
    const expirar = () => salir()
    window.addEventListener('fishy:sesion-vencida', expirar)
    return () => window.removeEventListener('fishy:sesion-vencida', expirar)
  }, [salir])
  const valor = useMemo<Sesion>(() => ({
    perfil, cargando, autenticado: perfil !== null,
    esProfesor: perfil?.rol === 'profesor',
    entrar, salir,
  }), [perfil, cargando, entrar, salir])
  return <ContextoSesion.Provider value={valor}>{children}</ContextoSesion.Provider>
}
