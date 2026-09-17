import type { TematicaId } from './reportes'

export type EstadoSeguimiento = 'prioritario' | 'apoyo' | 'sin_alertas' | 'muestra_insuficiente' | 'sin_datos'
export interface CriteriosSeguimiento {
  umbral_apoyo: number
  umbral_prioridad: number
  minimo_decisiones: number
  ventana_decisiones: number
  minimo_decisiones_general: number
  minimo_tematicas_general: number
  dias_datos_antiguos: number
}
export interface TemaSeguimiento {
  tematica: TematicaId
  estado: EstadoSeguimiento
  decisiones_evaluadas: number
  decisiones_seguras: number | null
  porcentaje_seguro: number | null
  actualizado_en: string | null
  datos_antiguos: boolean
}
export interface AlumnoSeguimiento {
  miembro_id: string
  nombre_nino: string
  email_familia: string
  estado: EstadoSeguimiento
  tematicas: TemaSeguimiento[]
  general: { estado: EstadoSeguimiento; decisiones_seguras: number; decisiones_evaluadas: number; porcentaje_seguro: number; tematicas_evaluadas: number } | null
  actualizado_en: string | null
}
/** Solo para el profesor del grupo. Nunca se incorpora al DTO del reporte/PDF agregado. */
export interface SeguimientoGrupo {
  grupo_id: string
  nombre_grupo: string
  consultado_en: string
  criterios: CriteriosSeguimiento
  alumnos: AlumnoSeguimiento[]
}
