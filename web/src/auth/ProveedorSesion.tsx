import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { login, logout, obtenerPerfil } from '@/api/auth'
import { DEMO_DISPONIBLE } from '@/lib/config'
import { getToken } from '@/lib/token'
import { CLAVE_SESION_DEMO, perfilesDemo, recuperarDemo } from '@/mocks/sesionDemo'
import { ContextoSesion } from './contexto'
import type { Sesion } from './contexto'
import type { AdultoResponsable } from '@/types/api'

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const generacion = useRef(0)
  const [demo, setDemo] = useState(() => DEMO_DISPONIBLE ? recuperarDemo() : null)
  const [perfil, setPerfil] = useState<AdultoResponsable | null>(demo)
  const [cargando, setCargando] = useState(() => !demo && getToken() !== null)
  useEffect(() => {
    if (demo || !getToken()) return
    const actual = generacion.current
    let vigente = true
    obtenerPerfil().then(datos => {
      if (vigente && actual === generacion.current) setPerfil(datos)
    }).catch(() => {
      if (vigente && actual === generacion.current) { logout(); setPerfil(null) }
    }).finally(() => { if (vigente && actual === generacion.current) setCargando(false) })
    return () => { vigente = false }
    // Recuperación inicial; las entradas posteriores las resuelven entrar/entrarDemo.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  const entrar = useCallback(async (nombre: string, password: string) => {
    const actual = ++generacion.current
    logout()
    try {
      await login(nombre, password)
      const datos = await obtenerPerfil()
      if (actual !== generacion.current) return
      try { sessionStorage.removeItem(CLAVE_SESION_DEMO) } catch { /* Sesión en memoria. */ }
      setDemo(null)
      setPerfil(datos)
    } catch (error) { if (actual === generacion.current) logout(); throw error }
    finally { if (actual === generacion.current) setCargando(false) }
  }, [])
  const entrarDemo = useCallback((cuenta: 'principal' | 'alternativa' = 'principal') => {
    if (!DEMO_DISPONIBLE) return
    generacion.current += 1
    logout()
    try { sessionStorage.setItem(CLAVE_SESION_DEMO, cuenta) } catch { /* Sesión en memoria. */ }
    setDemo(perfilesDemo[cuenta])
    setPerfil(perfilesDemo[cuenta])
    setCargando(false)
  }, [])
  const salir = useCallback(() => {
    generacion.current += 1
    logout()
    try { sessionStorage.removeItem(CLAVE_SESION_DEMO) } catch { /* Sesión en memoria. */ }
    setDemo(null)
    setPerfil(null)
  }, [])
  useEffect(() => {
    const expirar = () => salir()
    window.addEventListener('fishy:sesion-vencida', expirar)
    return () => window.removeEventListener('fishy:sesion-vencida', expirar)
  }, [salir])
  const valor = useMemo<Sesion>(() => ({
    perfil, cargando, autenticado: perfil !== null, modoDemo: demo !== null,
    esAdmin: perfil?.is_admin === true,
    entrar, entrarDemo, salir,
  }), [perfil, demo, cargando, entrar, entrarDemo, salir])
  return <ContextoSesion.Provider value={valor}>{children}</ContextoSesion.Provider>
}
