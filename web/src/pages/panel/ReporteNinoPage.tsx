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
import { simularProgreso } from '@/mocks/gruposMock'
import { TEMATICAS } from '@/types/reportes'
import type { TematicaId } from '@/types/reportes'

export default function ReporteNinoPage() {
  const { id } = useParams()
  const ninoId = Number(id)
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const estado = useDatosVivos(signal => panel.obtenerReporteNino(ninoId, { signal }), 'reporte:' + perfil?.id + ':' + modoDemo + ':' + id)
  const [tema, setTema] = useState<TematicaId>('retos_virales')
  const [simulando, setSimulando] = useState(false)
  const [mensaje, setMensaje] = useState('')
  const [error, setError] = useState<Error | null>(null)
  async function simular() {
    if (simulando || !perfil) return
    setSimulando(true); setMensaje(''); setError(null)
    try { await simularProgreso(perfil.id, ninoId, tema); setMensaje('Nuevo progreso de demostración registrado. El reporte se actualiza automáticamente.') }
    catch (e) { setError(comoError(e)) } finally { setSimulando(false) }
  }
  const r = estado.datos
  const resultados = tematicasCompletas(r?.tematicas ?? [])
  return <>
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/"><Icono nombre="volver" /> Reportes</Link><span>/</span><span>Reporte individual</span></nav>
    {estado.cargando && <Cargando mensaje="Cargando el reporte…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {r && <>
      <div className="encabezado"><div className="report-intro"><span className="avatar">{r.nino.nombre.slice(0, 1)}</span><div><span className="eyebrow">REPORTE INDIVIDUAL</span><h1>El progreso de {r.nino.nombre}</h1><p className="muted">Un resumen de su aprendizaje en seguridad digital.</p></div></div></div>
      <div className="kpis"><Estadistica etiqueta="Temáticas con resultados" valor={resultados.filter(t => t.metricas).length + ' de 3'} pie="con decisiones registradas" /><Estadistica etiqueta="Temáticas completadas" valor={resultados.filter(t => t.metricas?.completada).length + ' de 3'} pie="del recorrido del juego" /><Estadistica etiqueta="Estado del reporte" valor={hayResultados(resultados) ? 'Disponible' : 'Sin datos'} pie={hayResultados(resultados) ? 'resumen por temática' : 'a la espera de su primera actividad'} /></div>
      <div className="section-heading"><h2>Decisiones seguras por temática</h2><Sincronizacion actualizando={estado.actualizando} actualizado={r.actualizado_en} error={!!estado.error} /></div>
      <Tematicas resultados={resultados} />
      <p className="report-method">El porcentaje corresponde a decisiones seguras sobre el total de decisiones evaluadas en cada temática. No mide cuánto del juego se ha completado.</p>
      <Privacidad />
      {modoDemo && <details className="demo-controls"><summary>Probar la actualización automática</summary><p>Simula que el juego registra cinco nuevas decisiones, cuatro de ellas seguras. Solo modifica los datos ficticios de esta demostración.</p>
        <div className="pila"><label className="campo"><span>Temática</span><select value={tema} onChange={e => setTema(e.target.value as TematicaId)} disabled={simulando}>{TEMATICAS.map(t => <option key={t.id} value={t.id}>{t.nombre}</option>)}</select></label><button type="button" className="boton" disabled={simulando} onClick={simular}>{simulando ? 'Registrando…' : 'Simular nivel completado'}</button></div>
        {mensaje && <Exito>{mensaje}</Exito>}{error && <ErrorAviso error={error} />}
      </details>}
    </>}
  </>
}
