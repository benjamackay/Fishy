import type { SeguimientoGrupo } from '@/types/grupos'
import type { TematicaId } from '@/types/reportes'

const ESTADOS_DE_ALERTA = new Set(['apoyo', 'prioritario'])

/** Áreas por reforzar de cada integrante con alerta, en el orden del servidor (prioritarios primero). */
export function areasDeApoyo(seguimiento: SeguimientoGrupo): Map<string, TematicaId[]> {
  const alertas = new Map<string, TematicaId[]>()
  for (const alumno of seguimiento.alumnos) {
    const areas = alumno.tematicas.filter(t => ESTADOS_DE_ALERTA.has(t.estado)).map(t => t.tematica)
    if (areas.length) alertas.set(alumno.miembro_id, areas)
  }
  return alertas
}
