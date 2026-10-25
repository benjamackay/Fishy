import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { PublicHeader } from '@/components/PublicHeader'
import { OttoBienvenida } from '@/components/OttoBienvenida'
import { Icono } from '@/components/Icono'
import { CodigoRecuperacion } from '@/components/CodigoRecuperacion'
import './login.css'
import './bienvenida.css'
import './recuperar-contrasena.css'

export default function RecuperarContrasenaPage() {
  const { state } = useLocation()
  const [email, setEmail] = useState('')
  const [error, setError] = useState(false)
  const [confirmado, setConfirmado] = useState(false)
  const correo = useRef<HTMLInputElement>(null)
  function enviar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const valido = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) && email.trim().length <= 254 && correo.current?.validity.valid
    if (!valido) { setError(true); correo.current?.focus(); return }
    setError(false)
    setEmail(email.trim()); setConfirmado(true)
  }
  return <div className="public-auth-page"><PublicHeader />
    <div className="auth-split">
      <aside className="auth-story"><span className="welcome-kicker">VOLVAMOS A LA AVENTURA</span><h2>A veces hace falta<br />un pequeño <em>recordatorio.</em></h2><p>Olvidar una contraseña nos pasa a todos. Te acompañamos para que puedas volver a tu cuenta.</p><OttoBienvenida /><div className="auth-story-footer"><Icono nombre="escudo" />Tu próxima conversación importante te está esperando.</div></aside>
      <section className="auth-card recovery-card" aria-label="Recuperación de contraseña">
        <Link to="/login" state={state} className="auth-back"><Icono nombre="volver" />Volver a iniciar sesión</Link>
        <span className="recovery-symbol"><Icono nombre={confirmado ? 'correo' : 'candado'} /></span>
        {confirmado ? <CodigoRecuperacion email={email} cambiarCorreo={() => { setConfirmado(false); setError(false) }} /> : <>
          <div className="auth-heading"><span className="welcome-kicker">RECUPERA TU ACCESO</span><h1>¿Olvidaste tu contraseña?</h1><p>Ingresa el correo electrónico asociado a tu cuenta para comenzar.</p></div>
          <form aria-label="Recuperar contraseña" onSubmit={enviar} noValidate>
            <div className="auth-fields"><div className="campo"><label htmlFor="recuperacion-email">Correo electrónico</label><input ref={correo} id="recuperacion-email" type="email" autoComplete="email" inputMode="email" placeholder="tu.correo@ejemplo.cl" required maxLength={254} value={email} onChange={e => { setEmail(e.target.value); setError(false) }} aria-invalid={error || undefined} aria-describedby={error ? 'recuperacion-error' : 'recuperacion-ayuda'} />
              {error ? <small id="recuperacion-error" className="auth-field-error" role="alert">Ingresa un correo electrónico válido, por ejemplo nombre@ejemplo.cl.</small> : <small id="recuperacion-ayuda">Usa el correo con el que te registraste en Fishy!.</small>}
            </div></div>
            <button className="boton boton--primario auth-submit full-width">Continuar<Icono nombre="flecha" /></button>
          </form>
          <p className="recovery-note">Por ahora puedes probar el formulario. Esta vista previa no envía correos.</p>
        </>}
        <p className="auth-switch">¿Ya la recordaste?<Link to="/login" state={state}>Iniciar sesión</Link></p>
      </section>
    </div>
    <footer className="auth-page-foot">¿Necesitas ayuda? <a href="mailto:contacto@fishygame.cl">contacto@fishygame.cl</a></footer>
  </div>
}
