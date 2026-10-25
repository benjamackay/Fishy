import { Link, useParams } from 'react-router-dom'
import { useState } from 'react'
import type { Periodo } from '@/lib/periodo'
import { FiltroPeriodo } from '@/components/FiltroPeriodo'
import { useSesion } from '@/auth/contexto'
import { usePanel, useEsDemoPeriodo } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso } from '@/components/Aviso'
import { Icono } from '@/components/Icono'
import { Estadistica, Privacidad, Sincronizacion, Tematicas } from '@/components/Reportes'
import { hayResultados, tematicasCompletas } from '@/lib/reportes'

export default function ReporteNinoPage() {
  const { id } = useParams()
  const ninoId = Number(id)
  const { perfil } = useSesion()
  const panel = usePanel()
  const [periodo, setPeriodo] = useState<Periodo | null>(null)
  const demo = useEsDemoPeriodo()
  const estado = useDatosVivos(signal => panel.obtenerReporteNino(ninoId, { signal, periodo: periodo ?? undefined }), 'reporte:' + perfil?.id + ':' + id + ':' + JSON.stringify(periodo))
  const r = estado.datos
  const resultados = tematicasCompletas(r?.tematicas ?? [])
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/"><Icono nombre="volver" /> Reportes</Link><span>/</span><span>Reporte individual</span></nav>
    <FiltroPeriodo periodo={periodo} cambiar={setPeriodo} habilitado={demo} />
    {estado.cargando && <Cargando mensaje="Cargando el reporte…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {r && <>
      <div className="encabezado"><div className="report-intro"><span className="avatar">{r.nino.nombre.slice(0, 1)}</span><div><h1>El progreso de {r.nino.nombre}</h1></div></div></div>
      <div className="kpis"><Estadistica etiqueta="Temáticas con resultados" valor={resultados.filter(t => t.metricas).length + ' de 3'} /><Estadistica etiqueta="Temáticas completadas" valor={resultados.filter(t => t.metricas?.completada).length + ' de 3'} /><Estadistica etiqueta="Estado del reporte" valor={hayResultados(resultados) ? 'Disponible' : 'Sin datos'} /></div>
      <div className="section-heading"><h2>Decisiones seguras por temática</h2><Sincronizacion actualizando={estado.actualizando} actualizado={r.actualizado_en} error={!!estado.error} /></div>
      <Tematicas resultados={resultados} porPeriodo={!!periodo} />
      <p className="report-method">El porcentaje corresponde a decisiones seguras sobre el total de decisiones evaluadas en cada temática. No mide cuánto del juego se ha completado.</p>
      <Privacidad />
    </>}
  </>
}
