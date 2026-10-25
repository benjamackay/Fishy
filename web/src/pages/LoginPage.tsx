import { useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { ErrorAviso } from '@/components/Aviso'
import { CampoContrasena } from '@/components/CampoContrasena'
import { FormularioRegistro } from '@/components/FormularioRegistro'
import { Icono } from '@/components/Icono'
import { PublicHeader } from '@/components/PublicHeader'
import { OttoBienvenida } from '@/components/OttoBienvenida'
import { ApiError } from '@/lib/api'
import { ErrorUsuario } from '@/lib/errores'
import type { InvitacionPublica } from '@/types/grupos'
import './login.css'
import './bienvenida.css'

export default function LoginPage({ invitacion, registro = false }: { invitacion?: InvitacionPublica; registro?: boolean } = {}) {
  const { entrar, autenticado, esProfesor, cargando } = useSesion()
  const ubicacion = useLocation()
  const navegar = useNavigate()
  const estado = ubicacion.state as { desde?: string; nombre?: string } | null
  const [registroInvitacion, setRegistroInvitacion] = useState(false)
  const esRegistro = invitacion ? registroInvitacion : registro
  const [nombre, setNombre] = useState(estado?.nombre ?? '')
  const [password, setPassword] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const ocupado = enviando || cargando
  const desde = estado?.desde
  const destino = desde?.startsWith('/') && !desde.startsWith('//') && !['/login', '/registro'].includes(desde) ? desde : esProfesor ? '/admin/grupos' : '/'
  if (autenticado) return <Navigate to={destino} replace />

  async function enviar(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (ocupado) return
    setEnviando(true); setError(null)
    try { await entrar(nombre.trim(), password) }
    catch (e) { setError(e instanceof ApiError && e.status === 401 ? new ErrorUsuario('Nombre de usuario o contraseña incorrectos.') : e instanceof Error ? e : new Error('No pudimos iniciar sesión.')) }
    finally { setEnviando(false) }
  }
  function cambiarInvitacion() {
    setRegistroInvitacion(v => !v); setPassword(''); setError(null)
  }
  return <div className="public-auth-page"><PublicHeader />
    <div className="auth-split">
      <aside className="auth-story"><span className="welcome-kicker">APRENDER JUGANDO. CRECER ACOMPAÑADOS.</span><h2>{esRegistro ? <>Las grandes aventuras<br />empiezan <em>contigo.</em></> : <>Acompañar también<br />es <em>aprender juntos.</em></>}</h2><p>{esRegistro ? 'Súmate a una comunidad que acompaña a niños y niñas en sus primeros pasos por el mundo digital.' : 'Vuelve a conectar con sus aprendizajes. Cada decisión es una oportunidad para conversar.'}</p><OttoBienvenida /><div className="auth-story-footer"><Icono nombre="escudo" />Un pequeño paso hacia un mundo digital más seguro.</div></aside>
      <section className="auth-card" aria-label={esRegistro ? 'Registro en Fishy' : 'Acceso a Fishy'}>
        <Link to="/" className="auth-back"><Icono nombre="volver" />Volver a Fishy!</Link>
        {invitacion && <div className="aviso"><strong>Invitación para {invitacion.nombre_nino}</strong><p>{invitacion.grupo} · {invitacion.profesor}</p><p>Inicia sesión o regístrate con <strong>{invitacion.email}</strong>. Después confirmarás únicamente el perfil de este niño.</p></div>}
        {esRegistro ? <FormularioRegistro emailInvitacion={invitacion?.email} ocupado={ocupado} cambiarOcupado={setEnviando} volverAlLogin={usuario => {
          if (invitacion) { setNombre(usuario); setRegistroInvitacion(false) }
          else navegar('/login', { state: { ...estado, nombre: usuario } })
        }} /> : <>
          <div className="auth-heading"><span className="welcome-kicker">QUÉ BUENO TENERTE DE VUELTA</span><h1>Iniciar sesión</h1><p>Tu espacio para acompañar sus aprendizajes.</p></div>
          <form onSubmit={enviar} aria-label="Iniciar sesión" aria-busy={ocupado}>
            <div className="auth-fields"><label className="campo"><span>Nombre de usuario</span><input value={nombre} onChange={e => setNombre(e.target.value)} autoComplete="username" required maxLength={150} disabled={ocupado} placeholder="Tu nombre de usuario" /></label><CampoContrasena id="login-password" etiqueta="Contraseña" valor={password} cambiar={setPassword} ocupado={ocupado} /></div>
            {error && <ErrorAviso error={error} />}
            <div className="auth-recovery-link"><Link to="/recuperar-contrasena" state={estado}>¿Olvidaste tu contraseña?</Link></div>
            <button className="boton boton--primario auth-submit full-width" disabled={ocupado || !nombre.trim()}>{enviando ? 'Iniciando sesión…' : 'Iniciar sesión'}{enviando ? <span className="spinner" aria-hidden="true" /> : <Icono nombre="flecha" />}</button>
          </form>
        </>}
        <p className="auth-switch">{esRegistro ? '¿Ya tienes una cuenta?' : '¿Primera vez por aquí?'}{invitacion ? <button type="button" className="inline-button" disabled={ocupado} onClick={cambiarInvitacion}>{esRegistro ? 'Iniciar sesión' : 'Registrarse'}</button> : <Link to={esRegistro ? '/login' : '/registro'} state={estado}>{esRegistro ? 'Iniciar sesión' : 'Registrarse'}</Link>}</p>
      </section>
    </div>
    <footer className="auth-page-foot">¿Necesitas ayuda? <a href="mailto:contacto@fishygame.cl">contacto@fishygame.cl</a></footer>
  </div>
}
