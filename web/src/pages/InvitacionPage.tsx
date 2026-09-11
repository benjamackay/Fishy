import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { consultarInvitacion, aceptarInvitacion, claveNombre } from '@/api/invitaciones'
import { listarJugadores } from '@/api/jugadores'
import { useSesion } from '@/auth/contexto'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso, Exito } from '@/components/Aviso'
import { Marca } from '@/components/Marca'
import { comoError, ErrorUsuario } from '@/lib/errores'
import type { InvitacionPublica, InvitacionAceptada } from '@/types/grupos'
import type { UsuarioJugador } from '@/types/api'
import LoginPage from './LoginPage'
import './invitacion.css'

export default function InvitacionPage() {
  const { hash } = useLocation()
  return <ContenidoInvitacion key={hash} token={hash.slice(1)} />
}

function ContenidoInvitacion({ token }: { token: string }) {
  const { perfil, autenticado, cargando, esAdmin, modoDemo, salir } = useSesion()
  const [datos, setDatos] = useState<InvitacionPublica | null>(null)
  const [jugadores, setJugadores] = useState<UsuarioJugador[] | null>(null)
  const [error, setError] = useState<Error | null>(null)
  const [errorAccion, setErrorAccion] = useState<Error | null>(null)
  const [seleccion, setSeleccion] = useState('')
  const [confirmar, setConfirmar] = useState(false)
  const [ocupado, setOcupado] = useState(false)
  const [aceptada, setAceptada] = useState<InvitacionAceptada | null>(null)
  const [intento, setIntento] = useState(0)
  const exito = useRef<HTMLElement>(null)

  useEffect(() => {
    const controller = new AbortController()
    if (cargando || !/^[A-Za-z0-9_-]{43}$/.test(token)) return
    consultarInvitacion(token, controller.signal).then(async invitacion => {
      let perfiles: UsuarioJugador[] | null = null
      if (autenticado && !esAdmin && !modoDemo && perfil?.email.toLowerCase() === invitacion.email.toLowerCase()) perfiles = await listarJugadores({ signal: controller.signal })
      if (!controller.signal.aborted) { setDatos(invitacion); setJugadores(perfiles) }
    }).catch(e => { if (!controller.signal.aborted) setError(comoError(e)) })
    return () => controller.abort()
  }, [token, autenticado, cargando, esAdmin, modoDemo, perfil?.id, perfil?.email, intento])
  useEffect(() => { if (aceptada) exito.current?.focus() }, [aceptada])

  async function aceptar(e: React.FormEvent) {
    e.preventDefault()
    if (ocupado || !confirmar || !seleccion) return
    setOcupado(true); setErrorAccion(null)
    try {
      setAceptada(await aceptarInvitacion({ token, confirmar: true,
        ...(seleccion === 'nuevo' ? { crear_perfil: true } : { jugador_id: Number(seleccion) }) }))
    } catch (e) { setErrorAccion(comoError(e)) } finally { setOcupado(false) }
  }
  const errorEnlace = /^[A-Za-z0-9_-]{43}$/.test(token) ? error : new ErrorUsuario('El enlace de invitación está incompleto. Abre el enlace del correo.')
  if (errorEnlace) return <section className="card invitation-card"><h1>No pudimos abrir la invitación</h1><ErrorAviso error={errorEnlace} onReintentar={() => { setError(null); setDatos(null); setJugadores(null); setIntento(v => v + 1) }} /><p className="muted">Si venció o los datos son incorrectos, pide al profesor que la reenvíe o corrija.</p><Link className="boton" to={autenticado ? '/' : '/login'}>Ir a mi cuenta</Link></section>
  if (!datos || cargando) return <Cargando mensaje="Comprobando la invitación…" />
  if (!autenticado) return <LoginPage invitacion={datos} />
  if (aceptada) return <section className="card invitation-card" ref={exito} tabIndex={-1}><Exito>Invitación aceptada</Exito><h1>{datos.nombre_nino} ya está vinculado a {aceptada.grupo}.</h1><p>Sus demás hermanos no se agregaron al curso. El perfil conserva su progreso y sigue perteneciendo a tu cuenta.</p><Link className="boton boton--primario" to={'/reportes/' + aceptada.jugador_id}>Ver su reporte</Link></section>
  const cuentaIncorrecta = modoDemo || esAdmin || perfil?.email.toLowerCase() !== datos.email.toLowerCase()
  const candidatos = jugadores?.filter(j => j.adulto === perfil?.id && claveNombre(j.nombre) === claveNombre(datos.nombre_nino)) ?? []
  return <section className="card invitation-card">
    <div className="brand"><Marca /></div><span className="eyebrow">INVITACIÓN A UN CURSO</span><h1>Confirma el perfil del niño</h1>
    <dl className="invitation-summary"><dt>Niño o niña</dt><dd>{datos.nombre_nino}</dd><dt>Curso</dt><dd>{datos.grupo}</dd><dt>Profesor</dt><dd>{datos.profesor}</dd><dt>Cuenta destinataria</dt><dd>{datos.email}</dd></dl>
    {cuentaIncorrecta ? <><p className="aviso" role="alert">{modoDemo ? 'Las cuentas de demostración no pueden aceptar invitaciones reales.' : 'Esta invitación debe aceptarse con la cuenta del padre o madre que recibió el correo.'}</p><button className="boton" onClick={salir}>Usar la cuenta destinataria</button></> : <form onSubmit={aceptar}>
      <p className="muted">Solo se vinculará {datos.nombre_nino}. El profesor podrá ver el resumen del grupo y un seguimiento de sus necesidades de apoyo por temática, con su nombre y tu correo para coordinar el acompañamiento. No verá conversaciones ni respuestas literales. Tus otros hijos quedarán fuera de este curso.</p>
      <label className="campo"><span>Perfil que corresponde a {datos.nombre_nino}</span><select value={seleccion} onChange={e => { setSeleccion(e.target.value); setConfirmar(false) }} required disabled={ocupado}>
        <option value="">Selecciona una opción</option>
        {candidatos.map(j => <option key={j.id} value={j.id}>{j.nombre}{j.edad ? ' · ' + j.edad + ' años' : ''} · conservar progreso</option>)}
        {!candidatos.length && <option value="nuevo">Crear el perfil de {datos.nombre_nino}</option>}
      </select></label>
      {!candidatos.length && !!jugadores?.length && <p className="mini muted">Si ya tiene un perfil con otro nombre, pide al profesor que corrija la invitación para conservar su progreso.</p>}
      <label className="invitation-confirm"><input type="checkbox" checked={confirmar} onChange={e => setConfirmar(e.target.checked)} disabled={ocupado || !seleccion} required /><span>Confirmo que soy responsable de {datos.nombre_nino}, que el perfil corresponde a ese niño y que puede incorporarse a {datos.grupo}.</span></label>
      {errorAccion && <ErrorAviso error={errorAccion} />}
      <button className="boton boton--primario full-width" disabled={ocupado || !confirmar || !seleccion}>{ocupado ? 'Vinculando…' : 'Aceptar y vincular a este niño'}</button>
      <p className="mini muted">Si no reconoces estos datos, no aceptes la invitación. Contacta al profesor para corregirla.</p>
    </form>}
  </section>
}
