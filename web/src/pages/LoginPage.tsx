import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { ErrorAviso } from '@/components/Aviso'
import { Icono } from '@/components/Icono'
import { Marca } from '@/components/Marca'
import { DEMO_DISPONIBLE } from '@/lib/config'

export default function LoginPage() {
  const { entrar, entrarDemo, autenticado, esAdmin } = useSesion()
  const ubicacion = useLocation()
  const [nombre, setNombre] = useState('')
  const [password, setPassword] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const desde = (ubicacion.state as { desde?: string } | null)?.desde
  const destino = desde?.startsWith('/') && !desde.startsWith('//') && desde !== '/login' ? desde : esAdmin ? '/admin/grupos' : '/'
  if (autenticado) return <Navigate to={destino} replace />
  async function enviar(evento: React.FormEvent) {
    evento.preventDefault()
    if (enviando) return
    setEnviando(true)
    setError(null)
    try { await entrar(nombre.trim(), password) }
    catch (e) { setError(e instanceof Error ? e : new Error('No pudimos iniciar sesión.')) }
    finally { setEnviando(false) }
  }
  return <section className="login-card">
    <div className="brand"><Marca /></div>
    <span className="eyebrow">ESPACIO DEL TUTOR</span>
    <h1>Qué bueno verte.</h1>
    <p className="muted">Inicia sesión para acompañar su aprendizaje y consultar los reportes.</p>
    <form onSubmit={enviar}>
      <label className="campo"><span>Nombre de usuario</span><input value={nombre} onChange={e => setNombre(e.target.value)} autoComplete="username" required maxLength={150} disabled={enviando} /></label>
      <label className="campo"><span>Contraseña</span><input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" required disabled={enviando} /></label>
      {error && <ErrorAviso error={error} />}
      <button className="boton boton--primario full-width" disabled={enviando || !nombre.trim()}>{enviando ? 'Iniciando sesión…' : 'Iniciar sesión'}<Icono nombre="flecha" /></button>
    </form>
    {DEMO_DISPONIBLE && <div className="demo-login"><span className="muted mini">Explora cada perfil con datos ficticios</span><button type="button" className="boton full-width" disabled={enviando} onClick={() => entrarDemo()}>Probar como padre</button><button type="button" className="boton full-width" disabled={enviando} onClick={() => entrarDemo('alternativa')}>Probar como profesor</button><p className="mini muted">Padres: reportes de sus hijos. Profesores: creación y gestión de grupos.</p></div>}
    <p className="login-privacy"><Icono nombre="candado" />Solo tendrás acceso a los reportes permitidos para tu cuenta.</p>
  </section>
}
