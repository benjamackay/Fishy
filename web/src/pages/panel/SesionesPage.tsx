import { Link } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { ErrorAviso } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { Estadistica, Privacidad, Sincronizacion } from '@/components/Reportes'
import { Icono } from '@/components/Icono'
import { fechaActualizacion, hayResultados, porcentajeSeguro } from '@/lib/reportes'
import { TEMATICAS } from '@/types/reportes'

export default function SesionesPage() {
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const estado = useDatosVivos(signal => panel.listarNinos({ signal }), 'ninos:' + perfil?.id + ':' + modoDemo)
  const datos = estado.datos
  const ultima = datos?.map(n => n.actualizado_en).filter((f): f is string => !!f).sort().at(-1) ?? null
  return <>
    <div className="encabezado"><div><span className="eyebrow">SU APRENDIZAJE, PASO A PASO</span><h1>Reportes</h1><p className="muted">Acompaña el progreso de los niños y niñas vinculados a tu cuenta.</p></div></div>
    {estado.cargando && <Cargando mensaje="Cargando tus reportes…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {datos && <>
      <div className="kpis"><Estadistica etiqueta="Niños y niñas" valor={datos.length} pie="vinculados a tu cuenta" /><Estadistica etiqueta="Con resultados" valor={datos.every(n => n.tematicas !== undefined) ? datos.filter(n => hayResultados(n.tematicas ?? [])).length : '—'} pie="con decisiones registradas" /><Estadistica etiqueta="Temáticas del juego" valor="3" pie="para una navegación segura" /></div>
      <div className="section-heading"><h2>Sus reportes</h2><Sincronizacion actualizando={estado.actualizando} actualizado={ultima} error={!!estado.error} /></div>
      {datos.length === 0 ? <section className="empty-state"><Icono nombre="grupo" /><h2>Aún no hay niños vinculados</h2><p>Cuando se vincule un perfil a tu cuenta desde el juego, podrás consultar su reporte aquí.</p></section> :
        <div className="rejilla">{datos.map(n => <article className="card profile-card" key={n.id}>
          <div className="profile-heading"><span className="avatar">{n.nombre.slice(0, 1)}</span><div><h3>{n.nombre}</h3><span className="mini muted">{n.edad ? n.edad + ' años' : 'Perfil vinculado'}</span></div></div>
          <div className="profile-body">{n.tematicas ? TEMATICAS.map(t => {
            const valor = porcentajeSeguro(n.tematicas?.find(r => r.tematica === t.id)?.metricas)
            return <div className="topic-mini" key={t.id}><span>{t.nombre}</span><span className={valor === null ? 'muted' : ''}>{valor === null ? 'Sin datos' : valor + '%'}</span></div>
          }) : <p className="mini muted">Consulta su reporte para ver los resultados por temática.</p>}</div>
          <div className="profile-meta"><Icono nombre="reloj" />{fechaActualizacion(n.actualizado_en)}</div>
          <Link className="boton full-width" to={'/reportes/' + n.id} aria-label={'Ver reporte de ' + n.nombre}>Ver reporte<Icono nombre="flecha" /></Link>
        </article>)}</div>}
      <Privacidad />
    </>}
  </>
}
