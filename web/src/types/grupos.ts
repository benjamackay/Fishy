/**
 * Contrato del panel de administracion (grupos).
 *
 * ⚠️ NADA DE ESTO EXISTE TODAVIA EN EL BACKEND. No hay modelo `Grupo`, ni
 * membresias, ni endpoints. Este archivo es la especificacion que Django tiene
 * que implementar; mientras tanto lo sirve el mock de `src/mocks/gruposMock.ts`
 * (ver `src/api/grupos.ts`, que es donde se cambia una cosa por la otra).
 *
 * Restriccion importante que condiciona el diseño: hoy TODAS las vistas de
 * partidas filtran por `usuario_jugador__adulto=request.user`, asi que un admin
 * no puede armar el avance de un grupo llamando a los endpoints existentes —
 * solo veria a sus propios hijos. Por eso el contrato incluye
 * `GET /grupos/{id}/avance/`: el agregado lo tiene que calcular el backend, no
 * el frontend.
 *
 * Endpoints esperados:
 *   GET    /grupos/                          -> Grupo[]
 *   POST   /grupos/                          -> Grupo          body: NuevoGrupo
 *   GET    /grupos/{id}/                     -> GrupoDetalle
 *   PATCH  /grupos/{id}/                     -> Grupo          body: Partial<NuevoGrupo>
 *   DELETE /grupos/{id}/                     -> 204
 *   GET    /grupos/{id}/candidatos/          -> MiembroCandidato[]
 *   POST   /grupos/{id}/miembros/            -> GrupoDetalle   body: { jugador_id }
 *   DELETE /grupos/{id}/miembros/{jugadorId}/-> 204
 *   GET    /grupos/{id}/avance/              -> AvanceGrupo
 *
 * Permiso: solo `AdultoResponsable.is_admin`. Ese flag ademas hay que exponerlo
 * en `AdultoResponsableSerializer.fields`, que hoy no lo incluye.
 */

export interface Grupo {
  id: number
  nombre: string
  descripcion: string
  /** Cuantos perfiles de menores tiene, para no pedir el detalle en la lista. */
  total_miembros: number
  fecha_creacion: string
}

export interface NuevoGrupo {
  nombre: string
  descripcion?: string
}

/** Un perfil de menor dentro de un grupo. */
export interface MiembroGrupo {
  jugador_id: number
  nombre: string
  edad: number | null
  /** Nombre del adulto responsable, para distinguir homonimos entre familias. */
  adulto: string
  fecha_ingreso: string
}

export interface GrupoDetalle extends Grupo {
  miembros: MiembroGrupo[]
}

/** Perfil que todavia no pertenece al grupo y se puede agregar. */
export interface MiembroCandidato {
  jugador_id: number
  nombre: string
  edad: number | null
  adulto: string
}

/**
 * Avance de un miembro. Los campos de riesgo mantienen el signo del backend:
 * **mas alto = mas seguro**.
 */
export interface AvanceMiembro {
  jugador_id: number
  nombre: string
  /** Promedio de `progreso` (0-100) de sus partidas. */
  progreso: number
  partidas: number
  misiones_completadas: number
  zonas_completadas: number
  riesgo_total: number
  respuestas: number
  oportunidades_mejora: number
  /** null si el perfil nunca ha jugado. */
  ultima_actividad: string | null
}

export interface AvanceGrupo {
  grupo_id: number
  /** Promedio simple del `progreso` de los miembros. */
  progreso_promedio: number
  miembros_con_actividad: number
  total_miembros: number
  misiones_completadas: number
  zonas_completadas: number
  riesgo_total: number
  respuestas: number
  oportunidades_mejora: number
  /** Detalle por miembro, para la tabla del panel. */
  avance_miembros: AvanceMiembro[]
}
