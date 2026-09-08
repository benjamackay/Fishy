/**
 * DTOs del backend Django. Espejo de `Backend/backend/api/serializers.py`;
 * el contrato completo esta en DOCS_JSON_API.md.
 */

/** Cuenta que inicia sesion. Los perfiles de menores no tienen credenciales. */
export interface AdultoResponsable {
  id: number
  nombre: string
  apellido: string
  email: string
  edad: number | null
  fecha_nacimiento: string | null
  fecha_creacion: string
  /**
   * Ojo: hoy el backend NO expone este campo (no esta en
   * AdultoResponsableSerializer.fields). Queda opcional para no mentir sobre la
   * respuesta real; ver `esAdmin` en src/auth/AuthContext.tsx.
   */
  is_admin?: boolean
}

export interface RespuestaLogin {
  token: string
  adulto_id: number
}

/** Perfil de un menor. El progreso cuelga de aca, no de la cuenta del adulto. */
export interface UsuarioJugador {
  id: number
  adulto: number
  nombre: string
  edad: number | null
  fecha_creacion: string
}

export interface Partida {
  id: number
  usuario_jugador: number
  nivel_riesgo: number | null
  /** Porcentaje 0-100. */
  progreso: number
  fecha_inicio: string
  fecha_update: string
}

export interface NivelRiesgo {
  id: number
  nombre: string
  descripcion: string
  puntaje: number
}

export type EstadoMision = 'en_curso' | 'completada' | string

export interface MisionProgreso {
  id: number
  mision_id: string
  estado: EstadoMision
  /** Vacio si el `mision_id` no esta en el catalogo. */
  nombre: string
  zona: string
  en_catalogo: boolean
  fecha_desbloqueo: string
  fecha_completada: string | null
}

export interface ZonaProgreso {
  id: number
  zona: string
  /** Siempre true: la fila solo existe si la zona se desbloqueo. */
  desbloqueada: boolean
  completada: boolean
  fecha_desbloqueo: string
  fecha_completada: string | null
}

export interface ItemInventario {
  id: number
  item_id: string
  cantidad: number
  fecha_agregado: string
  fecha_actualizacion: string
}

/**
 * Riesgo por zona. **Signo: mas alto = mas seguro.** Un total negativo
 * significa que el menor eligio mayoritariamente respuestas inseguras.
 */
export interface RiesgoZona {
  zona: string
  riesgo_acumulado: number
  respuestas: number
  /** Cotas de esas mismas preguntas eligiendo siempre la peor / mejor opcion. */
  minimo_posible: number
  maximo_posible: number
}

export interface RiesgoPorZona {
  partida_id: number
  zonas: RiesgoZona[]
  total: number
  respuestas: number
  sin_clasificar: number
}

export interface OpcionElegida {
  opcion_banco_id: string
  texto: string
  impacto_puntuacion: number
  consecuencia: string
}

/** Una decision insegura, con la alternativa que se le escapo al menor. */
export interface OportunidadMejora {
  fecha: string
  zona: string
  npc: string
  pregunta_banco_id: string
  mensaje_npc: string
  eligio: OpcionElegida
  mejor_opcion: OpcionElegida
}

export interface OportunidadesMejora {
  partida_id: number
  jugador: string
  oportunidades: OportunidadMejora[]
}
