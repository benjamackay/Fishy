import { TEMATICAS } from '@/types/reportes'
import type { ResultadoTematica } from '@/types/reportes'
import { fechaActualizacion, porcentajeSeguro, tematicasCompletas } from '@/lib/reportes'
import { Icono } from './Icono'

export function Estadistica({ etiqueta, valor, pie }: { etiqueta: string; valor: React.ReactNode; pie?: string }) {
  return <div className="kpi"><span className="kpi__label">{etiqueta}</span><strong className="kpi__value">{valor}</strong>{pie && <span className="kpi__foot">{pie}</span>}</div>
}
export function Privacidad({ grupal = false }: { grupal?: boolean }) {
  return <aside className="privacy-note"><Icono nombre="escudo" /><p>{grupal ? 'Resultados agregados, sin datos personales ni resultados individuales.' : 'Este reporte no incluye conversaciones ni respuestas textuales.'}</p></aside>
}
export function Sincronizacion({ actualizando, actualizado, error = false }: { actualizando: boolean; actualizado: string | null; error?: boolean }) {
  return <div className="sync-status"><Icono nombre="reloj" /><span role="status">{error ? 'Actualización pendiente' : actualizando ? 'Consultando resultados…' : 'Actualización automática'}</span><span>· {fechaActualizacion(actualizado)}</span></div>
}
export function Tematicas({ resultados, grupal = false }: { resultados: ResultadoTematica[]; grupal?: boolean }) {
  const temas = tematicasCompletas(resultados)
  return <div className="topic-grid">{TEMATICAS.map(t => {
    const resultado = temas.find(r => r.tematica === t.id)!
    const valor = porcentajeSeguro(resultado.metricas)
    return <section className="card topic-card" key={t.id} aria-labelledby={'tema-' + t.id}>
      <div className="topic-icon"><Icono nombre={t.icono} /></div><h3 id={'tema-' + t.id}>{t.nombre}</h3><p className="topic-description">{t.descripcion}</p>
      {valor === null ? <div className="topic-empty"><strong>No hay datos disponibles</strong><p>{resultado.motivo === 'muestra_insuficiente' ? 'Aún no hay suficientes participantes con resultados para mostrar esta temática.' : 'Los resultados aparecerán cuando se registren decisiones en esta temática.'}</p></div> : <div>
        {!grupal && <span className={'badge ' + (resultado.metricas?.completada ? 'badge--success' : 'badge--warm')}>{resultado.metricas?.completada ? 'Temática completada' : 'En curso'}</span>}
        <p className="topic-score">{valor}<small>%</small></p><span className="mini muted">de decisiones seguras{grupal ? ' del grupo' : ''}</span>
        <div className="meter" role="meter" aria-label={'Decisiones seguras: ' + t.nombre} aria-valuenow={valor} aria-valuemin={0} aria-valuemax={100}><span style={{ width: valor + '%' }} /></div>
        <p className="topic-note">{resultado.metricas!.decisiones_seguras} de {resultado.metricas!.decisiones_evaluadas} decisiones evaluadas</p>
      </div>}
    </section>
  })}</div>
}
