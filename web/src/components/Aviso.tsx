import { ApiError } from '@/lib/api'

/**
 * Traduce un fallo a algo accionable. Los casos que mas se dan trabajando en
 * local son el backend apagado (fetch reventado, sin `status`) y el token
 * vencido (401), y cada uno pide una accion distinta.
 */
function explicar(error: Error): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'Tu sesion vencio. Vuelve a entrar.'
    if (error.status === 403) return 'Tu cuenta no tiene permiso para ver esto.'
    if (error.status === 404) return 'No encontramos lo que buscabas.'
    if (error.status >= 500) return 'El backend respondio con un error interno.'
    return `El backend respondio ${error.status}.`
  }
  return 'No pudimos contactar al backend. Revisa que Django este corriendo en 127.0.0.1:8000.'
}

export function ErrorAviso({
  error,
  onReintentar,
}: {
  error: Error
  onReintentar?: () => void
}) {
  return (
    <div className="aviso aviso--error" role="alert">
      <div>{explicar(error)}</div>
      {onReintentar && (
        <p style={{ margin: '0.5rem 0 0' }}>
          <button type="button" className="boton" onClick={onReintentar}>
            Reintentar
          </button>
        </p>
      )}
    </div>
  )
}

export function Vacio({ children }: { children: React.ReactNode }) {
  return <p className="vacio">{children}</p>
}
