import { api, ApiError } from '@/lib/api'
import { setToken, clearToken } from '@/lib/token'
import type { AdultoResponsable, RegistroAdulto, RespuestaLogin } from '@/types/api'
import { ErrorUsuario } from '@/lib/errores'

/** El login es con `nombre`, no con email (ver DOCS_JSON_API.md). */
export async function login(
  nombre: string,
  password: string,
): Promise<RespuestaLogin> {
  const datos = await api.post<RespuestaLogin>('/auth/login/', {
    nombre,
    password,
  })
  setToken(datos.token)
  return datos
}

export function logout(): void {
  clearToken()
}

export function obtenerPerfil(): Promise<AdultoResponsable> {
  return api.get<AdultoResponsable>('/auth/perfil/')
}

export async function operacionCuenta<T>(accion: () => Promise<T>): Promise<T> {
  try { return await accion() }
  catch (e) {
    if (e instanceof ApiError && e.data && typeof e.data === 'object') {
      const datos = e.data as Record<string, unknown>
      const errores = datos.errors && typeof datos.errors === 'object' ? datos.errors : datos
      const mensajes = Object.values(errores).flat().filter((v): v is string => typeof v === 'string')
      throw new ErrorUsuario(typeof datos.detail === 'string' ? [datos.detail, ...Object.values(datos.errors ?? {}).flat()].join(' ') : mensajes.join(' ') || 'Revisa los datos enviados.', e.status)
    }
    throw e
  }
}

export const guardarPerfil = (datos: Pick<AdultoResponsable, 'nombre' | 'apellido' | 'email' | 'fecha_nacimiento'>) =>
  operacionCuenta(() => api.patch<AdultoResponsable>('/auth/perfil/', datos))
export const cambiarContrasena = (password_actual: string, password_nueva: string, password_confirmacion: string) =>
  operacionCuenta(() => api.post<{ requiere_login: boolean }>('/auth/cambiar-contrasena/', { password_actual, password_nueva, password_confirmacion }))
export const solicitarRecuperacion = (email: string) => operacionCuenta(() => api.post<{
  solicitud_id: string; reenvio_en_segundos: number; expira_en_segundos: number
}>('/auth/recuperacion/solicitar/', { email }))
export const verificarRecuperacion = (solicitud_id: string, codigo: string) => operacionCuenta(() =>
  api.post<{ reset_token: string }>('/auth/recuperacion/verificar/', { solicitud_id, codigo }))
export const restablecerContrasena = (reset_token: string, password_nueva: string, password_confirmacion: string) =>
  operacionCuenta(() => api.post('/auth/recuperacion/restablecer/', { reset_token, password_nueva, password_confirmacion }))

/** Usa el endpoint existente. El registro no inicia una sesión ni asigna roles. */
export async function registrarCuenta(datos: RegistroAdulto): Promise<void> {
  const respuesta = await api.post<RespuestaLogin>('/auth/registro/', datos)
  if (!respuesta || typeof respuesta.token !== 'string' || !respuesta.token.trim() || !Number.isSafeInteger(respuesta.adulto_id) || respuesta.adulto_id <= 0) {
    throw new ErrorUsuario('No pudimos confirmar el registro. Si ya creaste tu cuenta, intenta iniciar sesión.')
  }
}
