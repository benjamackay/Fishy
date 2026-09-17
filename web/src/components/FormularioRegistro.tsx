import { useEffect, useRef, useState } from 'react'
import { registrarCuenta } from '@/api/auth'
import { ApiError } from '@/lib/api'
import { ErrorUsuario } from '@/lib/errores'
import { ErrorAviso } from './Aviso'
import { CampoContrasena } from './CampoContrasena'
import { Icono } from './Icono'

function errorRegistro(error: unknown): Error {
  if (error instanceof ApiError) {
    if (error.status === 400 || error.status === 409) {
      const campos = error.data && typeof error.data === 'object' ? error.data : {}
      if ('password' in campos) return new ErrorUsuario('La contraseña no cumple los requisitos del servicio. Prueba con otra.')
      return new ErrorUsuario('Revisa los datos ingresados. El nombre de usuario o el correo podrían estar registrados.')
    }
    if (error.status === 404 || error.status === 405) return new ErrorUsuario('El registro aún no está disponible. Inténtalo más adelante.')
  }
  return error instanceof Error ? error : new Error('No pudimos crear la cuenta.')
}

export function FormularioRegistro({ ocupado, cambiarOcupado, volverAlLogin, emailInvitacion }: {
  emailInvitacion?: string
  ocupado: boolean
  cambiarOcupado: (valor: boolean) => void
  volverAlLogin: (nombre: string) => void
}) {
  const [nombre, setNombre] = useState('')
  const [apellido, setApellido] = useState('')
  const [email, setEmail] = useState(emailInvitacion ?? '')
  const [nacimiento, setNacimiento] = useState('')
  const [password, setPassword] = useState('')
  const [confirmacion, setConfirmacion] = useState('')
  const [revisado, setRevisado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [creada, setCreada] = useState(false)
  const nombreInput = useRef<HTMLInputElement>(null)
  const confirmacionInput = useRef<HTMLInputElement>(null)
  const exito = useRef<HTMLDivElement>(null)
  const [fechaLocal] = useState(() => {
    const ahora = new Date()
    return new Date(ahora.getTime() - ahora.getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
  })
  const errorConfirmacion = revisado && password !== confirmacion ? 'Las contraseñas deben coincidir.' : undefined

  useEffect(() => { if (creada) exito.current?.focus() }, [creada])

  async function enviar(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (ocupado) return
    setError(null)
    setRevisado(true)
    if (!nombre.trim()) {
      setError(new ErrorUsuario('Ingresa un nombre de usuario.'))
      nombreInput.current?.focus()
      return
    }
    if (password !== confirmacion) { confirmacionInput.current?.focus(); return }
    cambiarOcupado(true)
    try {
      await registrarCuenta({ nombre: nombre.trim(), email: email.trim(), password,
        ...(apellido.trim() ? { apellido: apellido.trim() } : {}),
        ...(nacimiento ? { fecha_nacimiento: nacimiento } : {}),
      })
      setPassword('')
      setConfirmacion('')
      setCreada(true)
    } catch (e) { setError(errorRegistro(e)) }
    finally { cambiarOcupado(false) }
  }

  if (creada) return <div className="auth-success" ref={exito} tabIndex={-1}>
    <span className="auth-success-icon"><Icono nombre="check" /></span>
    <div role="status"><h1>Tu cuenta está lista.</h1><p className="muted">Ya puedes iniciar sesión con <strong>{nombre.trim()}</strong>.</p></div>
    <button type="button" className="boton boton--primario auth-submit full-width" onClick={() => volverAlLogin(nombre.trim())}>Ir a iniciar sesión<Icono nombre="flecha" /></button>
  </div>

  return <>
    <div className="auth-heading"><span className="eyebrow">COMIENZA TU RECORRIDO</span><h1>Crea tu cuenta.</h1><p className="muted">Completa tus datos para comenzar.</p></div>
    <form onSubmit={enviar} aria-label="Crear una cuenta" aria-busy={ocupado}>
      <div className="auth-fields auth-fields--register">
        <div className="campo auth-wide"><label htmlFor="registro-nombre">Nombre de usuario</label><input id="registro-nombre" ref={nombreInput} value={nombre} onChange={e => setNombre(e.target.value)} autoComplete="username" required maxLength={150} disabled={ocupado} aria-describedby="registro-nombre-ayuda" /><small id="registro-nombre-ayuda">Lo usarás para iniciar sesión.</small></div>
        <label className="campo"><span>Apellido <small>(opcional)</small></span><input value={apellido} onChange={e => setApellido(e.target.value)} autoComplete="family-name" maxLength={150} disabled={ocupado} /></label>
        <label className="campo"><span>Fecha de nacimiento <small>(opcional)</small></span><input type="date" value={nacimiento} onChange={e => setNacimiento(e.target.value)} autoComplete="bday" max={fechaLocal} disabled={ocupado} /></label>
        <label className="campo auth-wide"><span>Correo electrónico</span><input type="email" value={emailInvitacion ?? email} onChange={e => setEmail(e.target.value)} autoComplete="email" required maxLength={254} disabled={ocupado} readOnly={!!emailInvitacion} /></label>
        <CampoContrasena id="registro-password" etiqueta="Contraseña" valor={password} cambiar={setPassword} ocupado={ocupado} nueva />
        <CampoContrasena id="registro-confirmacion" etiqueta="Confirmar contraseña" valor={confirmacion} cambiar={setConfirmacion} ocupado={ocupado} nueva error={errorConfirmacion} inputRef={confirmacionInput} />
      </div>
      <p className="auth-password-hint">Usa al menos 4 caracteres; una frase larga será más fácil de recordar.</p>
      {error && <ErrorAviso error={error} />}
      <button className="boton boton--primario auth-submit full-width" disabled={ocupado}>{ocupado ? 'Creando tu cuenta…' : 'Crear cuenta'}{ocupado ? <span className="spinner" aria-hidden="true" /> : <Icono nombre="flecha" />}</button>
    </form>
  </>
}
