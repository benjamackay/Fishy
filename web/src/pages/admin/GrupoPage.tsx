import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso, Exito } from '@/components/Aviso'
import { Modal } from '@/components/Modal'
import { Icono } from '@/components/Icono'
import { SeguimientoGrupo } from '@/components/SeguimientoGrupo'
import { comoError } from '@/lib/errores'
import { fechaActualizacion } from '@/lib/reportes'
import type { InvitacionGrupo, MiembroGrupo } from '@/types/grupos'
import '../invitacion.css'

type Eliminacion = { miembro: MiembroGrupo } | { invitacion: InvitacionGrupo } | 'grupo'
const estadoInvitacion = (i: InvitacionGrupo) => i.estado === 'aceptada' ? 'Aceptada' : i.estado === 'cancelada' ? 'Cancelada' : i.estado === 'vencida' ? 'Vencida' : i.estado_envio === 'fallido' ? 'No se pudo enviar' : i.estado_envio === 'enviando' ? 'Enviando correo' : i.estado_envio === 'simulado' ? 'Pendiente · demostración' : 'Pendiente de aceptación'

export default function GrupoPage() {
  const { id = '' } = useParams()
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const navegar = useNavigate()
  const ubicacion = useLocation()
  const estado = useDatosVivos(signal => panel.obtenerGrupo(id, { signal }), 'grupo:' + perfil?.id + ':' + modoDemo + ':' + id)
  const [agregar, setAgregar] = useState(false)
  const [eliminar, setEliminar] = useState<Eliminacion | null>(null)
  const [email, setEmail] = useState('')
  const [nombreNino, setNombreNino] = useState('')
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [exito, setExito] = useState((ubicacion.state as { creado?: boolean } | null)?.creado ? 'Grupo creado correctamente. Ya puedes invitar a las familias.' : '')
  const g = estado.datos
  function abrirAgregar() { setEmail(''); setNombreNino(''); setError(null); setExito(''); setAgregar(true) }
  function abrirEliminar(objeto: Eliminacion) { setError(null); setExito(''); setEliminar(objeto) }
  async function alAgregar(e: React.FormEvent) {
    e.preventDefault()
    if (ocupado) return
    setOcupado(true); setError(null); setExito('')
    try {
      const invitacion = await panel.invitarFamilia(id, { email: email.trim().toLowerCase(), nombre_nino: nombreNino.trim() })
      setAgregar(false)
      setExito(invitacion.estado_envio === 'simulado' ? 'Invitación simulada. No se envió ningún correo.' : 'Correo de invitación enviado. El niño se incorporará cuando su padre o madre lo acepte.')
    } catch (e) { setError(comoError(e)) }
    finally { setOcupado(false); estado.recargar() }
  }
  async function reenviar(inv: InvitacionGrupo) {
    if (ocupado) return
    setOcupado(true); setError(null); setExito('')
    try {
      const nueva = await panel.reenviarInvitacion(id, inv.id)
      setExito(nueva.estado_envio === 'simulado' ? 'Reenvío simulado. No se envió ningún correo.' : 'Invitación reenviada. El enlace anterior dejó de ser válido.')
    } catch (e) { setError(comoError(e)) }
    finally { setOcupado(false); estado.recargar() }
  }
  async function confirmarEliminar() {
    if (!eliminar || ocupado) return
    setOcupado(true); setError(null)
    try {
      if (eliminar === 'grupo') {
        await panel.eliminarGrupo(id)
        navegar('/admin/grupos', { replace: true })
      } else if ('invitacion' in eliminar) {
        await panel.cancelarInvitacion(id, eliminar.invitacion.id)
        setEliminar(null); setExito('Invitación cancelada. Su enlace ya no permite vincular al niño.'); estado.recargar()
      } else {
        await panel.eliminarUsuario(id, eliminar.miembro.id)
        setEliminar(null); setExito('Niño eliminado del grupo. Su perfil y progreso se conservan.'); estado.recargar()
      }
    } catch (e) { setError(comoError(e)) } finally { setOcupado(false) }
  }
  const tituloEliminar = eliminar === 'grupo' ? 'Eliminar grupo' : eliminar && 'invitacion' in eliminar ? 'Cancelar invitación' : 'Eliminar integrante'
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/admin/grupos">Mis grupos</Link><span>/</span><span>Gestionar grupo</span></nav>
    {estado.cargando && <Cargando mensaje="Cargando el grupo…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {error && !agregar && !eliminar && <ErrorAviso error={error} />}
    {g && <>
      <div className="encabezado"><div><span className="eyebrow">ADMINISTRACIÓN DEL GRUPO</span><h1>{g.nombre}</h1><p className="muted">{g.descripcion || 'Gestiona los integrantes de este grupo.'}</p></div><Link className="boton boton--primario" to={'/admin/grupos/' + id + '/reporte'}><Icono nombre="reportes" />Ver reporte</Link></div>
      {exito && <Exito>{exito}</Exito>}
      <p className="group-id">Identificador único: {g.id}</p>
      <SeguimientoGrupo key={id} id={id} miembros={g.miembros} />
      <section className="card">
        <div className="pila spread"><div><h2>Niños del curso</h2><span className="mini muted">{g.miembros.length} {g.miembros.length === 1 ? 'perfil vinculado' : 'perfiles vinculados'}</span></div><button type="button" className="boton" disabled={ocupado} onClick={abrirAgregar}><Icono nombre="correo" />Invitar familia</button></div>
        {g.miembros.length === 0 ? <div className="empty-state" style={{ marginTop: 22 }}><Icono nombre="grupo" /><h3>Invita a la primera familia</h3><p>Indica el nombre del niño y el correo de su padre o madre. Cada invitación vincula a un solo niño.</p></div> :
          <ul className="members">{g.miembros.map(m => <li className="member" key={m.id}><span className="avatar small"><Icono nombre="grupo" /></span><div className="member-content"><strong>{m.nombre_nino}</strong><p>{m.email}</p><p>Vinculado el {fechaActualizacion(m.fecha_ingreso)}</p></div><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={() => abrirEliminar({ miembro: m })} aria-label={'Eliminar integrante ' + m.nombre_nino}><Icono nombre="borrar" />Eliminar</button></li>)}</ul>}
      </section>
      {!!g.invitaciones.length && <section className="card invitation-list" aria-label="Invitaciones del grupo"><h2>Invitaciones</h2><p className="mini muted">Las invitaciones pendientes no aportan resultados al reporte.</p><ul className="members">{g.invitaciones.map(i => <li className="member" key={i.id}>
        <div className="member-content"><strong>{i.nombre_nino}</strong><p>{i.email}</p><p><span className="badge">{estadoInvitacion(i)}</span></p>{['pendiente', 'vencida'].includes(i.estado) && <p>Vence: {fechaActualizacion(i.vence_en)}</p>}</div>
        {['pendiente', 'vencida'].includes(i.estado) && <div className="invitation-actions"><button type="button" className="boton" disabled={ocupado} onClick={() => reenviar(i)} aria-label={'Reenviar invitación de ' + i.nombre_nino}>Reenviar</button><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={() => abrirEliminar({ invitacion: i })} aria-label={'Cancelar invitación de ' + i.nombre_nino}>Cancelar</button></div>}
      </li>)}</ul></section>}
      <aside className="privacy-note"><Icono nombre="candado" /><p>El padre confirma qué perfil corresponde al niño invitado. Sus demás hijos no ingresan al curso. El seguimiento de apoyo es privado para el profesor del grupo. El reporte y el PDF utilizan únicamente resultados agregados.</p></aside>
      <div className="danger-zone"><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={() => abrirEliminar('grupo')}>Eliminar grupo</button></div>
    </>}
    {agregar && <Modal titulo="Invitar familia" cerrar={() => setAgregar(false)} ocupado={ocupado}><p className="mini muted">Envía una invitación por cada niño del curso. El padre puede tener una cuenta o crearla al recibir el correo.</p>
      <form onSubmit={alAgregar}>
        <label className="campo"><span>Nombre del niño o niña</span><input autoFocus value={nombreNino} onChange={e => setNombreNino(e.target.value)} maxLength={150} required disabled={ocupado} /></label>
        <p className="mini muted">Usa el nombre de su perfil de Fishy si ya juega. Si el nombre no coincide, la familia deberá pedirte una corrección.</p>
        <label className="campo"><span>Correo del padre o madre</span><input type="email" autoComplete="email" inputMode="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="familia@ejemplo.cl" maxLength={254} required disabled={ocupado} /></label>
        {modoDemo && <p className="aviso">Demostración: se guardará una invitación ficticia y no se enviará correo.</p>}
        {error && <ErrorAviso error={error} />}
        <div className="modal-actions"><button type="button" className="boton" disabled={ocupado} onClick={() => setAgregar(false)}>Cancelar</button><button className="boton boton--primario" disabled={ocupado || !email.trim() || !nombreNino.trim()}>{ocupado ? 'Enviando…' : modoDemo ? 'Simular invitación' : 'Enviar invitación'}</button></div>
      </form>
    </Modal>}
    {eliminar && <Modal titulo={tituloEliminar} cerrar={() => setEliminar(null)} ocupado={ocupado}>
      <p>{eliminar === 'grupo' ? 'Se eliminará el grupo y se invalidarán sus invitaciones. Las cuentas y los perfiles infantiles se conservan.' : 'invitacion' in eliminar ? <>¿Quieres cancelar la invitación de <strong>{eliminar.invitacion.nombre_nino}</strong>? Para corregir sus datos, cancélala y envía una nueva.</> : <>¿Quieres quitar a <strong>{eliminar.miembro.nombre_nino}</strong> de este curso? Su perfil y su progreso se conservarán.</>}</p>
      {error && <ErrorAviso error={error} />}
      <div className="modal-actions"><button autoFocus type="button" className="boton" disabled={ocupado} onClick={() => setEliminar(null)}>Volver</button><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={confirmarEliminar}>{ocupado ? 'Guardando…' : 'Confirmar'}</button></div>
    </Modal>}
  </>
}
