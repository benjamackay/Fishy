import { crearPanelDemo } from './gruposMock'
import { TEMATICAS } from '@/types/reportes'
import type { ResultadoTematica, TematicaId } from '@/types/reportes'
import type { Consulta } from '@/types/panel'
import { errorPeriodo, fechaChile } from '@/lib/periodo'
import type { Periodo } from '@/lib/periodo'
import { ErrorUsuario } from '@/lib/errores'

interface Decision { nino: number; tematica: TematicaId; registrada_en: string; segura: boolean; evaluada: boolean }
// Cada registro representa una decisión evaluada, nunca un porcentaje preagregado.
export const decisionesDemo: Decision[] = [101, 102, 103, 904, 905, 906].flatMap(nino => TEMATICAS.flatMap(t => [
  { nino, tematica: t.id, registrada_en: '2026-09-10T09:00:00-03:00', segura: true, evaluada: true },
  { nino, tematica: t.id, registrada_en: '2026-10-05T23:59:59-03:00', segura: false, evaluada: true },
  { nino, tematica: t.id, registrada_en: '2026-10-15T00:00:00-03:00', segura: true, evaluada: true },
  { nino, tematica: t.id, registrada_en: '2026-08-01T12:00:00-04:00', segura: false, evaluada: false },
])).concat([103, 904, 905, 906].flatMap(nino => TEMATICAS.flatMap(t =>
  t.id === 'ciberacoso' && [905, 906].includes(nino) ? [] : [true, true, false].map((segura, i) => ({ nino, tematica: t.id, registrada_en: `2026-10-18T1${i}:00:00-03:00`, segura, evaluada: true })),
)))
export function decisionesEnPeriodo(ids: number[], periodo?: Periodo) {
  if (periodo) { const error = errorPeriodo(periodo.inicio, periodo.fin); if (error) throw new ErrorUsuario(error) }
  return decisionesDemo.filter(d => d.evaluada && ids.includes(d.nino) && (!periodo || (fechaChile(d.registrada_en) >= periodo.inicio && fechaChile(d.registrada_en) <= periodo.fin)))
}
export function agregarDecisiones(ids: number[], periodo?: Periodo, minimo = 1): ResultadoTematica[] {
  const decisiones = decisionesEnPeriodo(ids, periodo)
  return TEMATICAS.map(t => {
    const filas = decisiones.filter(d => d.tematica === t.id)
    const participantes = new Set(filas.map(d => d.nino)).size
    if (!filas.length || participantes < minimo) return { tematica: t.id, metricas: null, motivo: filas.length ? 'muestra_insuficiente' : 'sin_resultados' }
    return { tematica: t.id, metricas: { decisiones_seguras: filas.filter(d => d.segura).length, decisiones_evaluadas: filas.length, completada: false } }
  })
}
const actualizado = (ids: number[], periodo?: Periodo) => decisionesEnPeriodo(ids, periodo).map(d => d.registrada_en).sort().at(-1) ?? null
export function crearPanelPeriodo(adultoId: number) {
  const base = crearPanelDemo(adultoId)
  const obtenerReporteNino = async (id: number, opciones?: Consulta) => {
    opciones?.signal?.throwIfAborted()
    const original = await base.obtenerReporteNino(id, opciones)
    return { ...original, periodo: opciones?.periodo ? { ...opciones.periodo, zona_horaria: 'America/Santiago' } : null, tematicas: agregarDecisiones([id], opciones?.periodo), actualizado_en: actualizado([id], opciones?.periodo) }
  }
  return { ...base, obtenerReporteNino,
    listarNinos: async (opciones?: Consulta) => Promise.all((await base.listarNinos(opciones)).map(async n => {
      const r = await obtenerReporteNino(n.id, opciones)
      return { ...n, tematicas: r.tematicas, actualizado_en: r.actualizado_en }
    })),
    obtenerReporteGrupo: async (id: string, opciones?: Consulta) => {
      opciones?.signal?.throwIfAborted()
      const original = await base.obtenerReporteGrupo(id, opciones)
      const detalle = await base.obtenerGrupo(id, opciones)
      const identificadores: Record<string, number> = { Sofía: 103, Lucas: 904, Emilia: 905, Agustín: 906 }
      const ids = detalle.miembros.map(m => identificadores[m.nombre_nino]).filter((id): id is number => id !== undefined)
      const filas = decisionesEnPeriodo(ids, opciones?.periodo)
      return { ...original, periodo: opciones?.periodo ? { ...opciones.periodo, zona_horaria: 'America/Santiago' } : null, tematicas: agregarDecisiones(ids, opciones?.periodo, Math.max(3, original.minimo_participantes)), participantes_con_resultados: new Set(filas.map(d => d.nino)).size, actualizado_en: actualizado(ids, opciones?.periodo) }
    },
  }
}
