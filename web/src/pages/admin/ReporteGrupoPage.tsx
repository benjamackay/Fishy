import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso, Exito } from '@/components/Aviso'
import { Icono } from '@/components/Icono'
import { Estadistica, Privacidad, Sincronizacion, Tematicas } from '@/components/Reportes'
import { hayResultados, tematicasCompletas } from '@/lib/reportes'
import { comoError } from '@/lib/errores'

export default function ReporteGrupoPage() {
  const { id = '' } = useParams()
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const estado = useDatosVivos(signal => panel.obtenerReporteGrupo(id, { signal }), 'reporte-grupo:' + perfil?.id + ':' + modoDemo + ':' + id)
  const [descargando, setDescargando] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [exito, setExito] = useState(false)
  const r = estado.datos
  const disponible = r && r.participantes_con_resultados >= Math.max(3, r.minimo_participantes) && hayResultados(r.tematicas)
  async function descargar() {
    if (descargando) return
    setDescargando(true); setError(null); setExito(false)
    try {
      // Reconsulta antes de exportar: el archivo usa el último agregado disponible.
      const ultimo = await panel.obtenerReporteGrupo(id)
      const { descargarPdfGrupo } = await import('@/lib/pdf')
      descargarPdfGrupo(ultimo, modoDemo)
      setExito(true)
      estado.recargar()
    } catch (e) { setError(comoError(e)) } finally { setDescargando(false) }
  }
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/admin/grupos">Mis grupos</Link><span>/</span><Link to={'/admin/grupos/' + id}>Gestionar grupo</Link><span>/</span><span>Reporte grupal</span></nav>
    {estado.cargando && <Cargando mensaje="Cargando el reporte grupal…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {r && <>
      <div className="encabezado"><div><span className="eyebrow">UNA MIRADA AL APRENDIZAJE COLECTIVO</span><h1>{r.nombre_grupo}</h1><p className="muted">Reporte grupal · Resumen de decisiones seguras por temática.</p></div><button type="button" className="boton boton--primario" disabled={!disponible || descargando || !!estado.error} onClick={descargar}><Icono nombre="descargar" />{descargando ? 'Generando PDF…' : 'Descargar reporte'}</button></div>
      {error && <ErrorAviso error={error} />}{exito && <Exito>PDF generado. La descarga está lista en tu navegador.</Exito>}
      {disponible ? <>
        <div className="kpis"><Estadistica etiqueta="Integrantes del grupo" valor={r.total_integrantes} pie="en este grupo" /><Estadistica etiqueta="Con resultados" valor={r.participantes_con_resultados} pie="aportan al resumen agregado" /><Estadistica etiqueta="Temáticas disponibles" valor={tematicasCompletas(r.tematicas).filter(t => t.metricas).length + ' de 3'} pie="con datos suficientes" /></div>
        <div className="section-heading"><h2>Decisiones seguras por temática</h2><Sincronizacion actualizando={estado.actualizando} actualizado={r.actualizado_en} error={!!estado.error} /></div>
        <Tematicas resultados={r.tematicas} grupal />
        <p className="report-method">Se suman las decisiones seguras y evaluadas del grupo por temática. Cada temática necesita al menos {Math.max(3, r.minimo_participantes)} participantes con resultados para mostrarse.</p>
      </> : <section className="empty-state"><Icono nombre="reportes" /><h2>Aún no hay datos disponibles para el análisis grupal</h2><p>El reporte aparecerá automáticamente cuando al menos {Math.max(3, r.minimo_participantes)} participantes tengan resultados en una misma temática.</p><Link className="boton" to={'/admin/grupos/' + id}>Gestionar integrantes</Link></section>}
      <Privacidad grupal />
    </>}
  </>
}
