import { api } from '@/lib/api'
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

/** Usa el endpoint existente. El registro no inicia una sesión ni asigna roles. */
export async function registrarCuenta(datos: RegistroAdulto): Promise<void> {
  const respuesta = await api.post<RespuestaLogin>('/auth/registro/', datos)
  if (!respuesta || typeof respuesta.token !== 'string' || !respuesta.token.trim() || !Number.isSafeInteger(respuesta.adulto_id) || respuesta.adulto_id <= 0) {
    throw new ErrorUsuario('No pudimos confirmar el registro. Si ya creaste tu cuenta, intenta iniciar sesión.')
  }
}
