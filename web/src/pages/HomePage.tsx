import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

type Estado = 'comprobando' | 'conectado' | 'sin-conexion'

const MENSAJES: Record<Estado, string> = {
  comprobando: 'Comprobando conexión con el backend…',
  conectado: 'Backend conectado.',
  'sin-conexion': 'Sin conexión con el backend. ¿Está corriendo Django?',
}

/**
 * Placeholder del panel. El ping a /health/ confirma que el proxy de Vite y el
 * cliente HTTP quedaron bien configurados; se puede borrar al armar el panel real.
 */
export default function HomePage() {
  const [estado, setEstado] = useState<Estado>('comprobando')

  useEffect(() => {
    let vigente = true

    api
      .get<{ status: string }>('/health/')
      .then(() => vigente && setEstado('conectado'))
      .catch(() => vigente && setEstado('sin-conexion'))

    return () => {
      vigente = false
    }
  }, [])

  return (
    <section className="card">
      <h1>Panel de Fishy!</h1>
      <p className="muted">
        Proyecto React recién creado. Las pantallas del panel parental van en{' '}
        <code>src/pages/</code>.
      </p>
      <p className={`estado estado--${estado}`}>{MENSAJES[estado]}</p>
    </section>
  )
}
