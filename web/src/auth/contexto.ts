import { createContext, useContext } from 'react'
import type { AdultoResponsable } from '@/types/api'

export interface Sesion {
  perfil: AdultoResponsable | null
  /** true mientras se resuelve el token guardado al arrancar la app. */
  cargando: boolean
  autenticado: boolean
  modoDemo: boolean
  entrarDemo: (cuenta?: 'principal' | 'alternativa') => void
  /** Solo true si el perfil declara explícitamente is_admin: true (profesor). */
  esAdmin: boolean
  entrar: (nombre: string, password: string) => Promise<void>
  salir: () => void
}

export const ContextoSesion = createContext<Sesion | null>(null)

export function useSesion(): Sesion {
  const sesion = useContext(ContextoSesion)
  if (!sesion) {
    throw new Error('useSesion se uso fuera de <ProveedorSesion>')
  }
  return sesion
}
