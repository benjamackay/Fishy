import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { ErrorAviso } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { Modal } from '@/components/Modal'
import { Icono } from '@/components/Icono'
import { comoError } from '@/lib/errores'

export default function GruposPage() {
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const navegar = useNavigate()
  const estado = useDatosVivos(signal => panel.listarGrupos({ signal }), 'grupos:' + perfil?.id + ':' + modoDemo)
  const [crear, setCrear] = useState(false)
  const [nombre, setNombre] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  function abrir() { setNombre(''); setDescripcion(''); setError(null); setCrear(true) }
  async function enviar(e: React.FormEvent) {
    e.preventDefault()
    if (ocupado || !nombre.trim()) return
    setOcupado(true); setError(null)
    try {
      const grupo = await panel.crearGrupo({ nombre: nombre.trim(), descripcion: descripcion.trim() })
      navegar('/admin/grupos/' + grupo.id, { state: { creado: true } })
    } catch (e) { setError(comoError(e)) } finally { setOcupado(false) }
  }
  return <>
    <div className="encabezado"><div><span className="eyebrow">APRENDER EN COMUNIDAD</span><h1>Mis grupos</h1><p className="muted">Organiza a tus integrantes y conoce cómo avanza el grupo.</p></div><button type="button" className="boton boton--primario" onClick={abrir}><Icono nombre="mas" />Crear grupo</button></div>
    {estado.cargando && <Cargando mensaje="Cargando tus grupos…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {estado.datos && <>
      <div className="section-heading"><h2>Todos los grupos</h2><span className="badge">{estado.datos.length} {estado.datos.length === 1 ? 'grupo' : 'grupos'}</span></div>
      {estado.datos.length === 0 ? <section className="empty-state"><Icono nombre="grupo" /><h2>Tu primer grupo comienza aquí</h2><p>Crea un grupo para gestionar integrantes y consultar su aprendizaje en conjunto.</p><button type="button" className="boton" onClick={abrir}>Crear mi primer grupo</button></section> :
        <div className="rejilla">{estado.datos.map(g => <article className="card group-card" key={g.id}>
          <div className="pila"><span className="avatar square"><Icono nombre="grupo" /></span><div><h3>{g.nombre}</h3><span className="mini muted">{g.total_miembros} {g.total_miembros === 1 ? 'integrante' : 'integrantes'}</span></div></div>
          <p className="mini muted">{g.descripcion || 'Sin descripción.'}</p>
          <div className="group-footer"><Link className="text-link" to={'/admin/grupos/' + g.id} aria-label={'Gestionar ' + g.nombre}>Gestionar grupo<Icono nombre="flecha" /></Link><Link className="boton" to={'/admin/grupos/' + g.id + '/reporte'} aria-label={'Ver reporte de ' + g.nombre}>Ver reporte</Link></div>
        </article>)}</div>}
    </>}
    {crear && <Modal titulo="Crear grupo" cerrar={() => setCrear(false)} ocupado={ocupado}><p className="muted mini">Elige un nombre para identificarlo. Luego podrás agregar a sus integrantes.</p>
      <form onSubmit={enviar}>
        <label className="campo"><span>Nombre del grupo</span><input autoFocus value={nombre} onChange={e => setNombre(e.target.value)} placeholder="Ej. 5° Básico A" required maxLength={80} disabled={ocupado} /></label>
        <label className="campo"><span>Descripción <span className="muted">(opcional)</span></span><textarea value={descripcion} onChange={e => setDescripcion(e.target.value)} maxLength={280} placeholder="¿Qué une a este grupo?" disabled={ocupado} /><small>{descripcion.length}/280 caracteres</small></label>
        {error && <ErrorAviso error={error} />}
        <div className="modal-actions"><button type="button" className="boton" disabled={ocupado} onClick={() => setCrear(false)}>Cancelar</button><button className="boton boton--primario" disabled={ocupado || !nombre.trim()}>{ocupado ? 'Creando…' : 'Crear grupo'}</button></div>
      </form>
    </Modal>}
  </>
}
