export class ErrorUsuario extends Error {
  status?: number
  constructor(mensaje: string, status?: number) { super(mensaje); this.name = 'ErrorUsuario'; this.status = status }
}
export function comoError(error: unknown): Error {
  return error instanceof Error ? error : new Error('Ocurrió un problema inesperado.')
}
