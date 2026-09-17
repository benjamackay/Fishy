export function Cargando({ mensaje = 'Cargando…' }: { mensaje?: string }) {
  return <div className="loading" role="status"><span className="spinner" aria-hidden="true" />{mensaje}</div>
}
