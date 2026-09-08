import { api } from '@/lib/api'
import { setToken, clearToken } from '@/lib/token'
import type { AdultoResponsable, RespuestaLogin } from '@/types/api'

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
