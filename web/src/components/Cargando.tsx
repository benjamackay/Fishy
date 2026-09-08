export function Cargando({ mensaje = 'Cargando...' }: { mensaje?: string }) {
  return (
    <p className="muted" role="status">
      {mensaje}
    </p>
  )
}
