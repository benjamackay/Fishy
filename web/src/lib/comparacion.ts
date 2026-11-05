import type { ReporteGrupo, ResultadoTematica } from '@/types/reportes'

export function motivoIndisponible(resultado: ResultadoTematica | undefined, reporte: ReporteGrupo): string | null {
  if (resultado?.motivo === 'muestra_insuficiente') return `Muestra insuficiente: se requieren al menos ${Math.max(3, reporte.minimo_participantes)} participantes con resultados en esta temática.`
  if (!resultado?.metricas || resultado.metricas.decisiones_evaluadas <= 0) return 'Sin decisiones evaluadas en esta temática durante el período.'
  if (reporte.participantes_con_resultados < Math.max(3, reporte.minimo_participantes)) return 'Muestra insuficiente de participantes en el período.'
  return null
}
export function porcentajeComparacion(resultado: ResultadoTematica | undefined, reporte: ReporteGrupo): number | null {
  if (motivoIndisponible(resultado, reporte)) return null
  return resultado!.metricas!.decisiones_seguras / resultado!.metricas!.decisiones_evaluadas * 100
}
export const numeroComparacion = (valor: number) => new Intl.NumberFormat('es-CL', { maximumFractionDigits: 1 }).format(valor)
