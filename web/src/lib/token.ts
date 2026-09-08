/**
 * Token de sesión del adulto responsable (DRF Token Auth).
 *
 * El backend lo devuelve al hacer login o registro y hay que mandarlo en cada
 * request autenticado como `Authorization: Token <token>`.
 */

const STORAGE_KEY = 'fishy.token'

export function getToken(): string | null {
  return localStorage.getItem(STORAGE_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(STORAGE_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(STORAGE_KEY)
}
