import { TEMATICAS } from '@/types/reportes'
import type { AlumnoSeguimiento, CriteriosSeguimiento, EstadoSeguimiento } from '@/types/seguimiento'
import type { MiembroGrupo } from '@/types/grupos'
import type { ReporteNino } from '@/types/reportes'

export const CRITERIOS_SEGUIMIENTO_DEMO: CriteriosSeguimiento = {
  umbral_apoyo: 60, umbral_prioridad: 40, minimo_decisiones: 5, ventana_decisiones: 20,
  minimo_decisiones_general: 10, minimo_tematicas_general: 2, dias_datos_antiguos: 30,
}
export const requiereApoyo = (estado: EstadoSeguimiento) => estado === 'prioritario' || estado === 'apoyo'
export const etiquetasSeguimiento: Record<EstadoSeguimiento, string> = {
  prioritario: 'Atención prioritaria', apoyo: 'Necesita apoyo', sin_alertas: 'Sin alertas en lo evaluado',
  muestra_insuficiente: 'Muestra insuficiente', sin_datos: 'Sin datos evaluables',
}
export const nombreTema = (id: string) => TEMATICAS.find(t => t.id === id)?.nombre ?? id
const orden: Record<EstadoSeguimiento, number> = { prioritario: 0, apoyo: 1, muestra_insuficiente: 2, sin_datos: 3, sin_alertas: 4 }
export const ordenarSeguimiento = (alumnos: AlumnoSeguimiento[]) => [...alumnos].sort((a, b) => orden[a.estado] - orden[b.estado] || a.nombre_nino.localeCompare(b.nombre_nino))

/** Solo datos ficticios. En modo real la evaluación y la ventana las resuelve el servidor. */
export function evaluarSeguimientoDemo(miembro: MiembroGrupo, reporte: ReporteNino | undefined, ahora: number): AlumnoSeguimiento {
  const reglas = CRITERIOS_SEGUIMIENTO_DEMO
  const nivel = (s: number, n: number): EstadoSeguimiento => s * 100 < n * reglas.umbral_prioridad ? 'prioritario' : s * 100 < n * reglas.umbral_apoyo ? 'apoyo' : 'sin_alertas'
  const fecha = reporte?.actualizado_en
  const fechaValida = fecha && Number.isFinite(Date.parse(fecha)) && Date.parse(fecha) <= ahora ? fecha : null
  const tematicas = TEMATICAS.map(t => {
    const m = reporte?.tematicas.find(v => v.tematica === t.id)?.metricas
    const valido = !!fechaValida && !!m && Number.isSafeInteger(m.decisiones_evaluadas) && m.decisiones_evaluadas > 0 && m.decisiones_evaluadas <= reglas.ventana_decisiones && Number.isSafeInteger(m.decisiones_seguras) && m.decisiones_seguras >= 0 && m.decisiones_seguras <= m.decisiones_evaluadas
    const n = valido ? m!.decisiones_evaluadas : 0
    const s = valido ? m!.decisiones_seguras : 0
    const suficiente = n >= reglas.minimo_decisiones
    return { tematica: t.id, estado: suficiente ? nivel(s, n) : n ? 'muestra_insuficiente' as const : 'sin_datos' as const,
      decisiones_evaluadas: n, decisiones_seguras: suficiente ? s : null,
      porcentaje_seguro: suficiente ? Math.round(s * 1000 / n) / 10 : null,
      actualizado_en: n ? fechaValida : null,
      datos_antiguos: !!n && !!fechaValida && Date.parse(fechaValida) < ahora - reglas.dias_datos_antiguos * 86400000 }
  })
  const evaluables = tematicas.filter(t => t.porcentaje_seguro !== null)
  const n = evaluables.reduce((s, t) => s + t.decisiones_evaluadas, 0)
  const s = evaluables.reduce((s, t) => s + t.decisiones_seguras!, 0)
  const general = evaluables.length >= reglas.minimo_tematicas_general && n >= reglas.minimo_decisiones_general
    ? { estado: nivel(s, n), decisiones_seguras: s, decisiones_evaluadas: n, porcentaje_seguro: Math.round(s * 1000 / n) / 10, tematicas_evaluadas: evaluables.length } : null
  const estados = [...tematicas.map(t => t.estado), ...(general ? [general.estado] : [])]
  const estado = (['prioritario', 'apoyo', 'sin_alertas', 'muestra_insuficiente'] as const).find(e => estados.includes(e)) ?? 'sin_datos'
  return { miembro_id: miembro.id, nombre_nino: miembro.nombre_nino, email_familia: miembro.email,
    estado, tematicas, general, actualizado_en: tematicas.some(t => t.actualizado_en) ? fechaValida : null }
}

/** Abre un borrador; la aplicación no envía mensajes ni cambia el seguimiento. */
export function correoSeguimiento(alumno: AlumnoSeguimiento, grupo: string): string | null {
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(alumno.email_familia) || /[\r\n]/.test(alumno.email_familia)) return null
  const temas = alumno.tematicas.filter(t => requiereApoyo(t.estado)).map(t => nombreTema(t.tematica)).join(', ')
  const subject = 'Conversar sobre el aprendizaje en Fishy · ' + grupo.replace(/[\r\n]/g, ' ')
  const body = ['Hola,', '', 'Me gustaría coordinar una conversación para acompañar a ' + alumno.nombre_nino + ' en su aprendizaje en Fishy.',
    temas ? 'Podemos revisar juntos las temáticas: ' + temas + '.' : 'Podemos revisar juntos sus decisiones recientes en el juego.',
    '¿Qué horario les acomoda?', '', 'Saludos.'].join('\n')
  return 'mailto:' + encodeURIComponent(alumno.email_familia) + '?subject=' + encodeURIComponent(subject) + '&body=' + encodeURIComponent(body)
}
