import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { ErrorAviso } from '@/components/Aviso'

interface EstadoRuta {
  desde?: string
}

/**
 * Login del adulto responsable. Ojo: el backend autentica por `nombre`, no por
 * email (ver DOCS_JSON_API.md). Los perfiles de los menores no tienen
 * credenciales propias.
 */
export default function LoginPage() {
  const { entrar, autenticado } = useSesion()
  const navegar = useNavigate()
  const ubicacion = useLocation()

  const [nombre, setNombre] = useState('')
  const [password, setPassword] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  const destino = (ubicacion.state as EstadoRuta | null)?.desde ?? '/'

  if (autenticado) return <Navigate to={destino} replace />

  async function alEnviar(evento: React.FormEvent) {
    evento.preventDefault()
    setEnviando(true)
    setError(null)
    try {
      await entrar(nombre, password)
      navegar(destino, { replace: true })
    } catch (e: unknown) {
      setError(e instanceof Error ? e : new Error(String(e)))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section className="card" style={{ maxWidth: '26rem', margin: '2rem auto' }}>
      <h1>Entrar</h1>
      <p className="muted mini">
        Con la cuenta del adulto responsable. Es la misma que se usa en el juego.
      </p>

      <form onSubmit={alEnviar} style={{ marginTop: '1.25rem' }}>
        <label className="campo">
          <span>Nombre</span>
          <input
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            autoComplete="username"
            required
            autoFocus
          />
        </label>

        <label className="campo">
          <span>Contrasena</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {error && (
          <div style={{ margin: '0.9rem 0' }}>
            <ErrorAviso error={error} />
          </div>
        )}

        <button
          type="submit"
          className="boton boton--primario"
          disabled={enviando}
        >
          {enviando ? 'Entrando...' : 'Entrar'}
        </button>
      </form>
    </section>
  )
}
