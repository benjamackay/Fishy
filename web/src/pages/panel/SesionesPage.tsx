import { Link } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { ErrorAviso } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { Estadistica, Sincronizacion } from '@/components/Reportes'
import { Icono } from '@/components/Icono'
import { fechaActualizacion, hayResultados, porcentajeSeguro } from '@/lib/reportes'
import { TEMATICAS } from '@/types/reportes'

export default function SesionesPage() {
  const { perfil } = useSesion()
  const panel = usePanel()
  const estado = useDatosVivos(signal => panel.listarNinos({ signal }), 'ninos:' + perfil?.id)
  const datos = estado.datos
  const incompletos = datos?.some(n => n.tematicas === undefined) ?? false
  const ultima = datos?.map(n => n.actualizado_en).filter((f): f is string => !!f).sort().at(-1) ?? null
  return <>
    <div className="encabezado"><div><h1>Reportes</h1></div></div>
    {estado.cargando && <Cargando mensaje="Cargando tus reportes…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {datos && <>
      <div className="kpis"><Estadistica etiqueta="Niños y niñas" valor={datos.length} pie="vinculados a tu cuenta" /><Estadistica etiqueta="Con resultados" valor={datos.every(n => n.tematicas !== undefined) ? datos.filter(n => hayResultados(n.tematicas ?? [])).length : '—'} pie="con decisiones registradas" /><Estadistica etiqueta="Temáticas del juego" valor="3" /></div>
      <div className="section-heading"><h2>Sus reportes</h2><Sincronizacion actualizando={estado.actualizando} actualizado={ultima} error={!!estado.error || incompletos} /></div>
      {incompletos && <p className="aviso" role="status">No se pudieron cargar todos los resúmenes. Puedes abrir cada reporte o <button type="button" className="text-link inline-button" onClick={estado.recargar}>reintentar</button>.</p>}
      {datos.length === 0 ? <section className="empty-state"><Icono nombre="grupo" /><h2>Aún no hay niños vinculados</h2><p>Cuando se vincule un perfil a tu cuenta desde el juego, podrás consultar su reporte aquí.</p></section> :
        <div className="rejilla">{datos.map(n => <article className="card profile-card" key={n.id}>
          <div className="profile-heading"><span className="avatar">{n.nombre.slice(0, 1)}</span><div><h3>{n.nombre}</h3>{n.edad !== null && <span className="mini muted">{n.edad} {n.edad === 1 ? 'año' : 'años'}</span>}</div></div>
          <div className="profile-body">{n.tematicas ? TEMATICAS.map(t => {
            const valor = porcentajeSeguro(n.tematicas?.find(r => r.tematica === t.id)?.metricas)
            return <div className="topic-mini" key={t.id}><span>{t.nombre}</span><span className={valor === null ? 'muted' : ''}>{valor === null ? 'Sin datos' : valor + '%'}</span></div>
          }) : <p className="mini muted">Resumen no disponible.</p>}</div>
          <div className="profile-meta"><Icono nombre="reloj" />{n.tematicas === undefined ? 'Actualización no disponible' : !hayResultados(n.tematicas) && !n.actualizado_en ? 'Sin decisiones registradas' : fechaActualizacion(n.actualizado_en)}</div>
          <Link className="boton full-width" to={'/reportes/' + n.id} aria-label={'Ver reporte de ' + n.nombre}>Ver reporte<Icono nombre="flecha" /></Link>
        </article>)}</div>}
    </>}
  </>
}
