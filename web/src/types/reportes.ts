export const TEMATICAS = [
  { id: 'desconocidos', nombre: 'Desconocidos', descripcion: 'Reconocer riesgos al interactuar con otras personas.', icono: 'escudo' },
  { id: 'ciberacoso', nombre: 'Ciberacoso', descripcion: 'Cuidar a otros y pedir ayuda frente al acoso.', icono: 'mensaje' },
  { id: 'retos_virales', nombre: 'Retos Virales', descripcion: 'Evaluar los riesgos antes de aceptar un reto.', icono: 'alerta' },
] as const
export type TematicaId = typeof TEMATICAS[number]['id']
export interface MetricasTematica {
  decisiones_seguras: number
  decisiones_evaluadas: number
  completada: boolean
}
export interface ResultadoTematica {
  tematica: TematicaId
  metricas: MetricasTematica | null
  motivo?: 'sin_resultados' | 'muestra_insuficiente'
}
export interface NinoResumen {
  id: number
  adulto_id: number
  nombre: string
  edad: number | null
  actualizado_en: string | null
  /** Ausente si el servicio de listado no incluye un resumen. */
  tematicas?: ResultadoTematica[]
}
export interface ReporteNino {
  nino: NinoResumen
  actualizado_en: string | null
  tematicas: ResultadoTematica[]
}
/** Nunca debe contener nombres, correos, identificadores ni resultados de integrantes. */
export interface ReporteGrupo {
  grupo_id: string
  nombre_grupo: string
  total_integrantes: number
  participantes_con_resultados: number
  minimo_participantes: number
  actualizado_en: string | null
  tematicas: ResultadoTematica[]
}
