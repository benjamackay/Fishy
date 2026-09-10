import { ApiError } from '@/lib/api'
import { ErrorUsuario } from '@/lib/errores'
function explicarError(error: Error): string {
  if (error instanceof ErrorUsuario) return error.message
  if (error instanceof ApiError) {
    if (error.status === 401) return 'Tu sesión venció. Vuelve a iniciar sesión.'
    if (error.status === 403) return 'Tu cuenta no tiene permiso para ver esta información.'
    if (error.status === 404) return 'No encontramos el elemento solicitado.'
    if (error.status === 409) return 'El usuario ya forma parte del grupo.'
    if (error.status === 400) return 'Revisa los datos ingresados e inténtalo nuevamente.'
    if (error.status >= 500) return 'El servicio no está disponible en este momento. Inténtalo nuevamente.'
  }
  return 'No pudimos completar la operación. Revisa tu conexión e inténtalo nuevamente.'
}
export function ErrorAviso({ error, onReintentar }: { error: Error; onReintentar?: () => void }) {
  return <div className="aviso aviso--error" role="alert"><div>{explicarError(error)}</div>{onReintentar && <button type="button" className="boton" onClick={onReintentar}>Reintentar</button>}</div>
}
export function Exito({ children }: { children: React.ReactNode }) { return <div className="aviso aviso--exito" role="status">{children}</div> }
export function Vacio({ children }: { children: React.ReactNode }) { return <p className="vacio">{children}</p> }
