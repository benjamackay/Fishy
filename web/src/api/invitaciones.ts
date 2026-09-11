import { api, ApiError } from '@/lib/api'
import { ErrorUsuario, comoError } from '@/lib/errores'
import type { AceptarInvitacion, InvitacionAceptada, InvitacionPublica } from '@/types/grupos'

/** Errores previstos del contrato de invitaciones, sin exponer respuestas HTML del servidor. */
export async function operacionGrupo<T>(operacion: () => Promise<T>): Promise<T> {
  try { return await operacion() }
  catch (e) {
    if (e instanceof ApiError && [400, 403, 409, 503].includes(e.status) && e.data && typeof e.data === 'object' && 'detail' in e.data && typeof e.data.detail === 'string') {
      throw new ErrorUsuario(e.data.detail)
    }
    throw comoError(e)
  }
}
export const consultarInvitacion = (token: string, signal?: AbortSignal) =>
  operacionGrupo(() => api.post<InvitacionPublica>('/invitaciones/consultar/', { token }, { signal }))
export const aceptarInvitacion = (datos: AceptarInvitacion) =>
  operacionGrupo(() => api.post<InvitacionAceptada>('/invitaciones/aceptar/', datos))

/** Los nombres solo filtran candidatos. La vinculación siempre requiere ID, propiedad y confirmación. */
export const claveNombre = (nombre: string) => nombre.normalize('NFC').trim().replace(/\s+/g, ' ').toLocaleLowerCase('es')
