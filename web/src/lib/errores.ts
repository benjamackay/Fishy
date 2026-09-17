export class ErrorUsuario extends Error {
  constructor(mensaje: string) { super(mensaje); this.name = 'ErrorUsuario' }
}
export function comoError(error: unknown): Error {
  return error instanceof Error ? error : new Error('Ocurrió un problema inesperado.')
}
