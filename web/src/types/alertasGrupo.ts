import type { TematicaId } from './reportes'

/** Señales cualitativas de presentación; no contiene puntajes ni porcentajes. */
export interface AlertaGrupo {
  id: string
  miembroId: string
  nombre: string
  area: TematicaId
  nivel: 'apoyo' | 'reforzar'
  observacion: string
  sugerencia: string
}
