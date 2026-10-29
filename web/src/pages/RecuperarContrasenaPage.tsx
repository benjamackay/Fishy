import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { PublicHeader } from '@/components/PublicHeader'
import { OttoBienvenida } from '@/components/OttoBienvenida'
import { Icono } from '@/components/Icono'
import { CodigoRecuperacion } from '@/components/CodigoRecuperacion'
import { solicitarRecuperacion, verificarRecuperacion, restablecerContrasena } from '@/api/auth'
import { CampoContrasena } from '@/components/CampoContrasena'
import './login.css'
import './bienvenida.css'
import './recuperar-contrasena.css'

export default function RecuperarContrasenaPage() {
  const { state } = useLocation()
  const [email, setEmail] = useState('')
  const [error, setError] = useState(false)
  const [confirmado, setConfirmado] = useState(false)
  const [ocupado, setOcupado] = useState(false)
  const [fallo, setFallo] = useState('')
  const [solicitud, setSolicitud] = useState('')
  const [resetToken, setResetToken] = useState('')
  const [claves, setClaves] = useState({ nueva: '', confirmar: '' })
  const [terminado, setTerminado] = useState(false)
  const [reenviarDesde, setReenviarDesde] = useState(0)
  const correo = useRef<HTMLInputElement>(null)
  async function enviar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const valido = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) && email.trim().length <= 254 && correo.current?.validity.valid
    if (!valido) { setError(true); correo.current?.focus(); return }
    setError(false)
    setOcupado(true); setFallo('')
    try {
      const datos = await solicitarRecuperacion(email.trim())
      setSolicitud(datos.solicitud_id); setEmail(email.trim()); setConfirmado(true)
      setReenviarDesde(Date.now() + datos.reenvio_en_segundos * 1000)
    } catch (e) { setFallo(e instanceof Error ? e.message : 'No se pudo solicitar el código.') }
    finally { setOcupado(false) }
  }
  async function restablecer(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setFallo('')
    if (claves.nueva.length < 8 || claves.nueva !== claves.confirmar) { setFallo('Usa al menos 8 caracteres y repite la misma contraseña.'); return }
    setOcupado(true)
    try { await restablecerContrasena(resetToken, claves.nueva, claves.confirmar); setResetToken(''); setSolicitud(''); setClaves({ nueva: '', confirmar: '' }); setTerminado(true) }
    catch (e) { setFallo(e instanceof Error ? e.message : 'No se pudo cambiar la contraseña.') }
    finally { setOcupado(false) }
  }
  return <div className="public-auth-page"><PublicHeader />
    <div className="auth-split">
      <aside className="auth-story"><span className="welcome-kicker">VOLVAMOS A LA AVENTURA</span><h2>A veces hace falta<br />un pequeño <em>recordatorio.</em></h2><p>Olvidar una contraseña nos pasa a todos. Te acompañamos para que puedas volver a tu cuenta.</p><OttoBienvenida /><div className="auth-story-footer"><Icono nombre="escudo" />Tu próxima conversación importante te está esperando.</div></aside>
      <section className="auth-card recovery-card" aria-label="Recuperación de contraseña">
        <Link to="/login" state={state} className="auth-back"><Icono nombre="volver" />Volver a iniciar sesión</Link>
        <span className="recovery-symbol"><Icono nombre={confirmado ? 'correo' : 'candado'} /></span>
        {terminado ? <div role="status"><h1>Contraseña actualizada</h1><p>Ya puedes iniciar sesión con tu nueva contraseña.</p><Link to="/login" state={state}>Iniciar sesión</Link></div> : resetToken ? <form onSubmit={restablecer} aria-label="Restablecer contraseña">
          <h1>Elige tu nueva contraseña</h1>
          <CampoContrasena id="reset-nueva" etiqueta="Nueva contraseña" valor={claves.nueva} cambiar={nueva => setClaves({ ...claves, nueva })} ocupado={ocupado} nueva />
          <CampoContrasena id="reset-confirmar" etiqueta="Confirmar contraseña" valor={claves.confirmar} cambiar={confirmar => setClaves({ ...claves, confirmar })} ocupado={ocupado} nueva />
          <button className="boton boton--primario" disabled={ocupado}>Guardar contraseña</button>
          <button type="button" className="boton" disabled={ocupado} onClick={() => { setResetToken(''); setSolicitud(''); setConfirmado(false); setClaves({ nueva: '', confirmar: '' }); setFallo('') }}>Pedir otro código</button>
        </form> : confirmado ? <>
          <CodigoRecuperacion email={email} cambiarCorreo={() => { setConfirmado(false); setSolicitud(''); setError(false); setFallo('') }} verificarCodigo={async codigo => { const datos = await verificarRecuperacion(solicitud, codigo); setFallo(''); setResetToken(datos.reset_token) }} />
          <button type="button" className="boton" disabled={ocupado} onClick={async () => {
            if (Date.now() < reenviarDesde) { setFallo('Espera ' + Math.ceil((reenviarDesde - Date.now()) / 1000) + ' segundos antes de reenviar.'); return }
            setOcupado(true); setFallo('')
            try { const datos = await solicitarRecuperacion(email); setSolicitud(datos.solicitud_id); setReenviarDesde(Date.now() + datos.reenvio_en_segundos * 1000) }
            catch (e) { setFallo(e instanceof Error ? e.message : 'No se pudo reenviar.') }
            finally { setOcupado(false) }
          }}>Reenviar código</button>
        </> : <>
          <div className="auth-heading"><span className="welcome-kicker">RECUPERA TU ACCESO</span><h1>¿Olvidaste tu contraseña?</h1><p>Ingresa el correo electrónico asociado a tu cuenta para comenzar.</p></div>
          <form aria-label="Recuperar contraseña" onSubmit={enviar} noValidate>
            <div className="auth-fields"><div className="campo"><label htmlFor="recuperacion-email">Correo electrónico</label><input ref={correo} id="recuperacion-email" type="email" autoComplete="email" inputMode="email" placeholder="tu.correo@ejemplo.cl" required maxLength={254} value={email} onChange={e => { setEmail(e.target.value); setError(false) }} aria-invalid={error || undefined} aria-describedby={error ? 'recuperacion-error' : 'recuperacion-ayuda'} />
              {error ? <small id="recuperacion-error" className="auth-field-error" role="alert">Ingresa un correo electrónico válido, por ejemplo nombre@ejemplo.cl.</small> : <small id="recuperacion-ayuda">Usa el correo con el que te registraste en Fishy!.</small>}
            </div></div>
            <button disabled={ocupado} className="boton boton--primario auth-submit full-width">Continuar<Icono nombre="flecha" /></button>
          </form>
          <p className="recovery-note">Si existe una cuenta asociada, recibirás un código. Revisa también spam.</p>
        </>}
        {fallo && <p className="aviso aviso--error" role="alert">{fallo}</p>}
        <p className="auth-switch">¿Ya la recordaste?<Link to="/login" state={state}>Iniciar sesión</Link></p>
      </section>
    </div>
    <footer className="auth-page-foot">¿Necesitas ayuda? <a href="mailto:contacto@fishygame.cl">contacto@fishygame.cl</a></footer>
  </div>
}
