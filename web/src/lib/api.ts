/**
 * Cliente HTTP del backend Django (DRF). El contrato está en DOCS_JSON_API.md.
 *
 * En desarrollo `VITE_API_URL` es `/api` y Vite reenvía esas rutas al Django
 * local (proxy en vite.config.ts), así que no hay CORS de por medio. En
 * producción se apunta la variable al backend real.
 */

import { getToken } from './token'

const BASE_URL: string = import.meta.env.VITE_API_URL ?? '/api'

/** Respuesta HTTP no exitosa. `data` trae el cuerpo que devolvió DRF. */
export class ApiError extends Error {
  status: number
  data: unknown

  constructor(status: number, data: unknown) {
    super(`La API respondió ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.data = data
  }
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
): Promise<T> {
  const headers = new Headers()
  const token = getToken()
  if (token) headers.set('Authorization', `Token ${token}`)
  if (body !== undefined) headers.set('Content-Type', 'application/json')

  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  const payload = await parseBody(response)
  if (!response.ok) throw new ApiError(response.status, payload)
  return payload as T
}

/** DRF puede responder JSON, texto plano o nada (204). */
async function parseBody(response: Response): Promise<unknown> {
  if (response.status === 204) return null
  const text = await response.text()
  if (!text) return null
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
}
