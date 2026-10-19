import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel, useEsDemoPeriodo } from '@/hooks/usePanel'
import { FiltroPeriodo } from '@/components/FiltroPeriodo'
import type { Periodo } from '@/lib/periodo'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso, Exito } from '@/components/Aviso'
import { Icono } from '@/components/Icono'
import { Estadistica, Privacidad, Sincronizacion, Tematicas } from '@/components/Reportes'
import { hayResultados, tematicasCompletas } from '@/lib/reportes'
import { comoError } from '@/lib/errores'

export default function ReporteGrupoPage() {
  const { id = '' } = useParams()
  const { perfil } = useSesion()
  const panel = usePanel()
  const [periodo, setPeriodo] = useState<Periodo | null>(null)
  const demo = useEsDemoPeriodo()
  const estado = useDatosVivos(signal => panel.obtenerReporteGrupo(id, { signal, periodo: periodo ?? undefined }), 'reporte-grupo:' + perfil?.id + ':' + id + ':' + JSON.stringify(periodo))
  const [descargando, setDescargando] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [exito, setExito] = useState(false)
  const r = estado.datos
  const disponible = r && r.participantes_con_resultados >= Math.max(3, r.minimo_participantes) && hayResultados(r.tematicas)
  async function descargar() {
    if (descargando || periodo) return
    setDescargando(true); setError(null); setExito(false)
    try {
      // Reconsulta antes de exportar: el archivo usa el último agregado disponible.
      const ultimo = await panel.obtenerReporteGrupo(id)
      const { descargarPdfGrupo } = await import('@/lib/pdf')
      descargarPdfGrupo(ultimo)
      setExito(true)
      estado.recargar()
    } catch (e) { setError(comoError(e)) } finally { setDescargando(false) }
  }
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/admin/grupos">Mis grupos</Link><span>/</span><Link to={'/admin/grupos/' + id}>Gestionar grupo</Link><span>/</span><span>Reporte grupal</span></nav>
    <div className="comparar-report-tools"><Link className="boton" to={'/admin/grupos/' + id + '/comparar'}><Icono nombre="reportes" />Comparar períodos</Link><FiltroPeriodo periodo={periodo} cambiar={p => { setPeriodo(p); setExito(false); setError(null) }} habilitado={demo} /></div>
    {estado.cargando && <Cargando mensaje="Cargando el reporte grupal…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {r && <>
      <div className="encabezado"><div><h1>{r.nombre_grupo}</h1><p className="muted">Reporte grupal</p></div><button type="button" className="boton boton--primario" disabled={!disponible || descargando || !!estado.error || !!periodo} onClick={descargar}><Icono nombre="descargar" />{descargando ? 'Generando PDF…' : 'Descargar reporte'}</button></div>
      {periodo && <p className="mini muted">La vista por período está disponible en pantalla. El PDF estará disponible cuando se conecte la exportación por fechas.</p>}
      {error && <ErrorAviso error={error} />}{exito && <Exito>PDF generado. Revisa las descargas de tu navegador.</Exito>}
      {disponible ? <>
        <div className="kpis"><Estadistica etiqueta="Integrantes del grupo" valor={r.total_integrantes} /><Estadistica etiqueta="Con resultados" valor={r.participantes_con_resultados} /><Estadistica etiqueta="Temáticas disponibles" valor={tematicasCompletas(r.tematicas).filter(t => t.metricas).length + ' de 3'} /></div>
        <div className="section-heading"><h2>Decisiones seguras por temática</h2><Sincronizacion actualizando={estado.actualizando} actualizado={r.actualizado_en} error={!!estado.error} /></div>
        <Tematicas resultados={r.tematicas} grupal porPeriodo={!!periodo} />
        <p className="report-method">Se suman las decisiones seguras y evaluadas del grupo por temática. Cada temática necesita al menos {Math.max(3, r.minimo_participantes)} participantes con resultados para mostrarse.</p>
      </> : <section className="empty-state"><Icono nombre="reportes" /><h2>{periodo ? 'Sin datos' : 'Aún no hay datos disponibles para el análisis grupal'}</h2><p>{periodo ? 'No hay suficientes decisiones evaluadas de participantes dentro del período para mostrar resultados grupales.' : 'El reporte aparecerá automáticamente cuando haya participantes suficientes con resultados.'} Se requieren al menos {Math.max(3, r.minimo_participantes)} participantes en una misma temática.</p><Link className="boton" to={'/admin/grupos/' + id}>Gestionar integrantes</Link></section>}
      <Privacidad grupal />
    </>}
  </>
}
