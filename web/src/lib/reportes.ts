import { TEMATICAS } from '@/types/reportes'
import type { MetricasTematica, ResultadoTematica } from '@/types/reportes'

export function porcentajeSeguro(metricas: MetricasTematica | null | undefined): number | null {
  if (!metricas) return null
  const { decisiones_seguras: seguras, decisiones_evaluadas: total } = metricas
  if (!Number.isSafeInteger(seguras) || !Number.isSafeInteger(total) || total <= 0 || seguras < 0 || seguras > total) return null
  return Math.round(seguras / total * 100)
}
export function tematicasCompletas(resultados: ResultadoTematica[]): ResultadoTematica[] {
  return TEMATICAS.map(t => {
    const r = resultados.find(r => r.tematica === t.id)
    return r && porcentajeSeguro(r.metricas) !== null ? r : { tematica: t.id, metricas: null, motivo: r?.motivo ?? 'sin_resultados' }
  })
}
export function hayResultados(resultados: ResultadoTematica[]): boolean {
  return resultados.some(r => porcentajeSeguro(r.metricas) !== null)
}
export function fechaActualizacion(valor: string | null): string {
  if (!valor || !Number.isFinite(Date.parse(valor))) return 'Sin actividad registrada'
  return new Intl.DateTimeFormat('es-CL', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(valor))
}
