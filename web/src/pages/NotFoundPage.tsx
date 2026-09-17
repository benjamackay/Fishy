import { Link } from 'react-router-dom'

export default function NotFoundPage() {
  return (
    <section className="card">
      <h1>Página no encontrada</h1>
      <p className="muted">La ruta que abriste no existe.</p>
      <Link to="/">Volver al inicio</Link>
    </section>
  )
}
