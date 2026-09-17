const STORAGE_KEY = 'fishy.token'
let tokenEnMemoria: string | null = null
export function getToken(): string | null {
  try { return localStorage.getItem(STORAGE_KEY) } catch { return tokenEnMemoria }
}
export function setToken(token: string): void {
  tokenEnMemoria = token
  try { localStorage.setItem(STORAGE_KEY, token) } catch { /* Sesión de esta pestaña. */ }
}
export function clearToken(): void {
  tokenEnMemoria = null
  try { localStorage.removeItem(STORAGE_KEY) } catch { /* Sesión de esta pestaña. */ }
}
