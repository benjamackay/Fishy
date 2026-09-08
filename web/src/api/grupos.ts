/**
 * Fachada de la API de grupos.
 *
 * Es el UNICO punto donde se elige entre el mock y los endpoints reales. Las
 * paginas siempre importan de aca, asi que cuando Django implemente los
 * endpoints (ver la especificacion en `@/types/grupos`) basta con poner
 * `VITE_GRUPOS_MOCK=false` en el .env: no se toca ni una pagina.
 */

import { api } from '@/lib/api'
import { USAR_MOCK_GRUPOS } from '@/lib/config'
import * as mock from '@/mocks/gruposMock'
import type {
  AvanceGrupo,
  Grupo,
  GrupoDetalle,
  MiembroCandidato,
  NuevoGrupo,
} from '@/types/grupos'

const real = {
  listarGrupos: () => api.get<Grupo[]>('/grupos/'),

  crearGrupo: (datos: NuevoGrupo) => api.post<Grupo>('/grupos/', datos),

  eliminarGrupo: (id: number) => api.delete<void>(`/grupos/${id}/`),

  obtenerGrupo: (id: number) => api.get<GrupoDetalle>(`/grupos/${id}/`),

  listarCandidatos: (id: number) =>
    api.get<MiembroCandidato[]>(`/grupos/${id}/candidatos/`),

  agregarMiembro: (grupoId: number, jugadorId: number) =>
    api.post<GrupoDetalle>(`/grupos/${grupoId}/miembros/`, {
      jugador_id: jugadorId,
    }),

  quitarMiembro: (grupoId: number, jugadorId: number) =>
    api.delete<void>(`/grupos/${grupoId}/miembros/${jugadorId}/`),

  obtenerAvanceGrupo: (id: number) =>
    api.get<AvanceGrupo>(`/grupos/${id}/avance/`),
}

const impl = USAR_MOCK_GRUPOS ? mock : real

export const listarGrupos = impl.listarGrupos
export const crearGrupo = impl.crearGrupo
export const eliminarGrupo = impl.eliminarGrupo
export const obtenerGrupo = impl.obtenerGrupo
export const listarCandidatos = impl.listarCandidatos
export const agregarMiembro = impl.agregarMiembro
export const quitarMiembro = impl.quitarMiembro
export const obtenerAvanceGrupo = impl.obtenerAvanceGrupo

/** Para avisar en la UI que los datos de grupos no son reales. */
export const GRUPOS_SON_MOCK = USAR_MOCK_GRUPOS
