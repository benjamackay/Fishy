import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso, Exito } from '@/components/Aviso'
import { Modal } from '@/components/Modal'
import { Icono } from '@/components/Icono'
import { AgregarNinos } from '@/components/AgregarNinos'
import { EVENTO_DATOS } from '@/hooks/useDatosVivos'
import { comoError } from '@/lib/errores'
import { fechaActualizacion } from '@/lib/reportes'
import type { MiembroGrupo } from '@/types/grupos'
import '../invitacion.css'

type Eliminacion = { miembro: MiembroGrupo } | 'grupo'
export default function GrupoPage() {
  const { id = '' } = useParams()
  const { perfil } = useSesion()
  const panel = usePanel()
  const navegar = useNavigate()
  const ubicacion = useLocation()
  const estado = useDatosVivos(signal => panel.obtenerGrupo(id, { signal }), 'grupo:' + perfil?.id + ':' + id)
  const [agregarNinos, setAgregarNinos] = useState(false)
  const [eliminar, setEliminar] = useState<Eliminacion | null>(null)
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [confirmacion, setConfirmacion] = useState<{ titulo: string; mensaje: string } | null>(null)
  const [exito, setExito] = useState((ubicacion.state as { creado?: boolean } | null)?.creado ? 'Grupo creado.' : '')
  const g = estado.datos
  function abrirEliminar(objeto: Eliminacion) { setError(null); setExito(''); setEliminar(objeto) }
  async function confirmarEliminar() {
    if (!eliminar || ocupado) return
    setOcupado(true); setError(null)
    try {
      if (eliminar === 'grupo') {
        await panel.eliminarGrupo(id)
        navegar('/admin/grupos', { replace: true })
      } else {
        await panel.eliminarUsuario(id, eliminar.miembro.id)
        setEliminar(null); setExito('Niño eliminado del grupo. Su perfil y progreso se conservan.'); estado.recargar()
      }
    } catch (e) { setError(comoError(e)) } finally { setOcupado(false) }
  }
  const tituloEliminar = eliminar === 'grupo' ? 'Eliminar grupo' : 'Eliminar integrante'
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/admin/grupos">Mis grupos</Link><span>/</span><span>Gestionar grupo</span></nav>
    {estado.cargando && <Cargando mensaje="Cargando el grupo…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {error && !eliminar && <ErrorAviso error={error} />}
    {g && <>
      <div className="encabezado"><div><h1>{g.nombre}</h1>{g.descripcion && <p className="muted">{g.descripcion}</p>}</div><div className="grupo-acciones"><Link className="boton boton--primario" to={'/admin/grupos/' + id + '/reporte'}><Icono nombre="reportes" />Ver reporte</Link><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={() => abrirEliminar('grupo')}><Icono nombre="borrar" />Eliminar grupo</button></div></div>
      {exito && <Exito>{exito}</Exito>}
      <section className="card">
        <div className="pila spread"><div><h2>Niños del curso</h2><span className="mini muted">{g.miembros.length} {g.miembros.length === 1 ? 'perfil vinculado' : 'perfiles vinculados'}</span></div><button type="button" className="boton" disabled={ocupado} onClick={() => { setError(null); setExito(''); setAgregarNinos(true) }}><Icono nombre="mas" />Agregar usuario</button>
        </div>
        {g.miembros.length === 0 ? <div className="empty-state" style={{ marginTop: 22 }}><Icono nombre="grupo" /><h3>Agrega a los primeros niños</h3><p>Busca el correo de su apoderado y selecciona los perfiles que corresponden a este curso.</p></div> :
          <ul className="members">{g.miembros.map(m => <li className="member" key={m.id}><span className="avatar small"><Icono nombre="grupo" /></span><div className="member-content"><strong>{m.nombre_nino}</strong><p>{m.email}</p><p>Vinculado el {fechaActualizacion(m.fecha_ingreso)}</p></div><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={() => abrirEliminar({ miembro: m })} aria-label={'Eliminar usuario ' + m.nombre_nino}><Icono nombre="borrar" />Eliminar usuario</button></li>)}</ul>}
      </section>
    </>}
    {agregarNinos && <AgregarNinos key={id} id={id} cerrar={() => setAgregarNinos(false)} alGuardar={() => {
      setAgregarNinos(false)
      setConfirmacion({ titulo: 'Niño(s) agregado(s) al curso', mensaje: 'Se agregó correctamente al niño o a los niños seleccionados. Ya forman parte del curso.' })
      estado.recargar()
      window.dispatchEvent(new Event(EVENTO_DATOS))
    }} />}
    {confirmacion && <Modal titulo={confirmacion.titulo} cerrar={() => setConfirmacion(null)}>
      <span className="avatar" style={{ display: 'flex', marginInline: 'auto' }}><Icono nombre="check" /></span>
      <p style={{ marginTop: 16 }}>{confirmacion.mensaje}</p>
      <div className="modal-actions"><button autoFocus type="button" className="boton boton--primario" onClick={() => setConfirmacion(null)}>Entendido</button></div>
    </Modal>}
    {eliminar && <Modal titulo={tituloEliminar} cerrar={() => setEliminar(null)} ocupado={ocupado}>
      <p>{eliminar === 'grupo' ? 'Se eliminará el grupo y se invalidarán sus invitaciones. Las cuentas y los perfiles infantiles se conservan.' : <>¿Quieres quitar a <strong>{eliminar.miembro.nombre_nino}</strong> de este curso? Su perfil y su progreso se conservarán.</>}</p>
      {error && <ErrorAviso error={error} />}
      <div className="modal-actions"><button autoFocus type="button" className="boton" disabled={ocupado} onClick={() => setEliminar(null)}>Volver</button><button type="button" className="boton boton--peligro" disabled={ocupado} onClick={confirmarEliminar}>{ocupado ? 'Guardando…' : 'Confirmar'}</button></div>
    </Modal>}
  </>
}
