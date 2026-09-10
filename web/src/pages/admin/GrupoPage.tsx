import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso, Exito } from '@/components/Aviso'
import { Modal } from '@/components/Modal'
import { Icono } from '@/components/Icono'
import { comoError, ErrorUsuario } from '@/lib/errores'
import { fechaActualizacion } from '@/lib/reportes'
import { correosDemo, normalizarCorreo } from '@/mocks/gruposMock'
import type { MiembroGrupo } from '@/types/grupos'

export default function GrupoPage() {
  const { id = '' } = useParams()
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const navegar = useNavigate()
  const ubicacion = useLocation()
  const estado = useDatosVivos(signal => panel.obtenerGrupo(id, { signal }), 'grupo:' + perfil?.id + ':' + modoDemo + ':' + id)
  const [agregar, setAgregar] = useState(false)
  const [eliminar, setEliminar] = useState<MiembroGrupo | 'grupo' | null>(null)
  const [email, setEmail] = useState('')
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [exito, setExito] = useState((ubicacion.state as { creado?: boolean } | null)?.creado ? 'Grupo creado correctamente. Ya puedes gestionar sus integrantes.' : '')
  const g = estado.datos
  function abrirAgregar() { setEmail(''); setError(null); setExito(''); setAgregar(true) }
  function abrirEliminar(miembro: MiembroGrupo | 'grupo') { setError(null); setExito(''); setEliminar(miembro) }
  async function alAgregar(e: React.FormEvent) {
    e.preventDefault()
    if (ocupado) return
    setOcupado(true); setError(null)
    try {
      const correo = normalizarCorreo(email)
      if (g?.miembros.some(m => normalizarCorreo(m.email) === correo)) throw new ErrorUsuario('El usuario ya forma parte del grupo.')
      await panel.agregarUsuario(id, correo)
      setAgregar(false); setEmail(''); setExito('Usuario agregado al grupo correctamente.')
      estado.recargar()
    } catch (e) { setError(comoError(e)) } finally { setOcupado(false) }
  }
  async function confirmarEliminar() {
    if (!eliminar || ocupado) return
    setOcupado(true); setError(null)
    try {
      if (eliminar === 'grupo') {
        await panel.eliminarGrupo(id)
        navegar('/admin/grupos', { replace: true })
      } else {
        await panel.eliminarUsuario(id, eliminar.id)
        setEliminar(null); setExito('Usuario eliminado del grupo correctamente.'); estado.recargar()
      }
    } catch (e) { setError(comoError(e)) } finally { setOcupado(false) }
  }
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/admin/grupos">Mis grupos</Link><span>/</span><span>Gestionar grupo</span></nav>
    {estado.cargando && <Cargando mensaje="Cargando el grupo…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {g && <>
      <div className="encabezado"><div><span className="eyebrow">ADMINISTRACIÓN DEL GRUPO</span><h1>{g.nombre}</h1><p className="muted">{g.descripcion || 'Gestiona los integrantes de este grupo.'}</p></div><Link className="boton boton--primario" to={'/admin/grupos/' + id + '/reporte'}><Icono nombre="reportes" />Ver reporte</Link></div>
      {exito && <Exito>{exito}</Exito>}
      <p className="group-id">Identificador único: {g.id}</p>
      <section className="card">
        <div className="pila spread"><div><h2>Integrantes</h2><span className="mini muted">{g.miembros.length} {g.miembros.length === 1 ? 'usuario en el grupo' : 'usuarios en el grupo'}</span></div><button type="button" className="boton" onClick={abrirAgregar}><Icono nombre="mas" />Agregar usuario</button></div>
        {g.miembros.length === 0 ? <div className="empty-state" style={{ marginTop: 22 }}><Icono nombre="correo" /><h3>El grupo está listo para recibir integrantes</h3><p>Agrega un usuario registrado con su correo electrónico.</p></div> :
          <ul className="members">{g.miembros.map(m => <li className="member" key={m.id}><span className="avatar small"><Icono nombre="correo" /></span><div className="member-content"><strong>{m.email}</strong><p>Agregado el {fechaActualizacion(m.fecha_ingreso)}</p></div><button type="button" className="boton boton--peligro" onClick={() => abrirEliminar(m)} aria-label={'Eliminar usuario ' + m.email}><Icono nombre="borrar" />Eliminar usuario</button></li>)}</ul>}
      </section>
      <aside className="privacy-note"><Icono nombre="candado" /><p>Los correos se utilizan para gestionar integrantes. El reporte grupal no incluye datos personales ni resultados individuales.</p></aside>
      <div className="danger-zone"><button type="button" className="boton boton--peligro" onClick={() => abrirEliminar('grupo')}>Eliminar grupo</button></div>
    </>}
    {agregar && <Modal titulo="Agregar usuario" cerrar={() => setAgregar(false)} ocupado={ocupado}><p className="mini muted">Ingresa el correo electrónico de un usuario registrado.</p>
      <form onSubmit={alAgregar}><label className="campo"><span>Correo electrónico</span><input autoFocus type="email" autoComplete="email" inputMode="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="usuario@ejemplo.cl" maxLength={254} required disabled={ocupado} /></label>
        {error && <ErrorAviso error={error} />}
        <div className="modal-actions"><button type="button" className="boton" disabled={ocupado} onClick={() => setAgregar(false)}>Cancelar</button><button className="boton boton--primario" disabled={ocupado || !email.trim()}>{ocupado ? 'Agregando…' : 'Agregar'}</button></div>
      </form>
      {modoDemo && <details className="demo-controls"><summary>Correos de la demostración</summary><p>Usuarios ficticios registrados; algunos ya pueden pertenecer al grupo.</p>{correosDemo(perfil!.id).map(c => <p className="mini" key={c}>{c}</p>)}</details>}
    </Modal>}
    {eliminar && <Modal titulo={eliminar === 'grupo' ? 'Eliminar grupo' : 'Eliminar usuario'} cerrar={() => setEliminar(null)} ocupado={ocupado}>
      <p>{eliminar === 'grupo' ? 'Se eliminará este grupo y su lista de integrantes. Esta acción no elimina las cuentas de los usuarios.' : <>¿Quieres eliminar a <strong>{eliminar.email}</strong> de este grupo?</>}</p>
      {eliminar !== 'grupo' && <p className="mini muted">Podrás volver a agregarlo con su correo electrónico.</p>}
      {error && <ErrorAviso error={error} />}
      <div className="modal-actions"><button autoFocus type="button" className="boton" disabled={ocupado} onClick={() => setEliminar(null)}>Cancelar</button><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={confirmarEliminar}>{ocupado ? 'Eliminando…' : 'Confirmar eliminación'}</button></div>
    </Modal>}
  </>
}
