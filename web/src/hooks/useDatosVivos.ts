import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '@/lib/api'
import { comoError, ErrorUsuario } from '@/lib/errores'

export const INTERVALO_REPORTES_MS = 15_000
export const EVENTO_DATOS = 'fishy:datos-actualizados'
interface Estado<T> { clave: string; datos: T | null; error: Error | null; actualizando: boolean }

/** Revalida al entrar, volver a la pestaña, recuperar conexión y recibir cambios.
 * Un evento durante una consulta se atiende al terminar; nunca se pisan respuestas.
 */
export function useDatosVivos<T>(cargar: (signal: AbortSignal) => Promise<T>, clave: string) {
  const [estado, setEstado] = useState<Estado<T> | null>(null)
  const [intento, setIntento] = useState(0)
  useEffect(() => {
    let vigente = true
    let consultando = false
    let pendiente = false
    const controller = new AbortController()
    async function consultar() {
      if (!vigente) return
      if (consultando) { pendiente = true; return }
      consultando = true
      setEstado(prev => ({ clave, datos: prev?.clave === clave ? prev.datos : null, error: null, actualizando: true }))
      try {
        const datos = await cargar(controller.signal)
        if (vigente && !pendiente) setEstado({ clave, datos, error: null, actualizando: false })
      } catch (e) {
        const error = comoError(e)
        if (vigente && !pendiente) setEstado(prev => ({
          clave, datos: error instanceof ErrorUsuario || (error instanceof ApiError && [401, 403, 404].includes(error.status)) ? null : prev?.clave === clave ? prev.datos : null,
          error, actualizando: false,
        }))
      } finally {
        consultando = false
        if (pendiente && vigente) { pendiente = false; void consultar() }
      }
    }
    const refrescar = () => { if (document.visibilityState !== 'hidden') void consultar() }
    void consultar()
    const timer = window.setInterval(refrescar, INTERVALO_REPORTES_MS)
    window.addEventListener('focus', refrescar)
    window.addEventListener('online', refrescar)
    window.addEventListener('storage', refrescar)
    window.addEventListener(EVENTO_DATOS, refrescar)
    document.addEventListener('visibilitychange', refrescar)
    return () => {
      vigente = false
      controller.abort()
      window.clearInterval(timer)
      window.removeEventListener('focus', refrescar)
      window.removeEventListener('online', refrescar)
      window.removeEventListener('storage', refrescar)
      window.removeEventListener(EVENTO_DATOS, refrescar)
      document.removeEventListener('visibilitychange', refrescar)
    }
    // La clave identifica cuenta + recurso. Nunca conserva datos entre cuentas/rutas.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave, intento])
  const recargar = useCallback(() => setIntento(n => n + 1), [])
  const actual = estado?.clave === clave ? estado : null
  return { datos: actual?.datos ?? null, error: actual?.error ?? null,
    cargando: !actual || (!actual.datos && actual.actualizando),
    actualizando: actual?.actualizando ?? true, recargar }
}
