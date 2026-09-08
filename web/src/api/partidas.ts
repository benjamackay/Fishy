import { api } from '@/lib/api'
import type {
  ItemInventario,
  MisionProgreso,
  NivelRiesgo,
  OportunidadesMejora,
  Partida,
  RiesgoPorZona,
  ZonaProgreso,
} from '@/types/api'

export function obtenerPartida(id: number): Promise<Partida> {
  return api.get<Partida>(`/partidas/${id}/`)
}

export function listarMisiones(id: number): Promise<MisionProgreso[]> {
  return api.get<MisionProgreso[]>(`/partidas/${id}/misiones/`)
}

export function listarZonas(id: number): Promise<ZonaProgreso[]> {
  return api.get<ZonaProgreso[]>(`/partidas/${id}/zonas/`)
}

export function listarInventario(id: number): Promise<ItemInventario[]> {
  return api.get<ItemInventario[]>(`/partidas/${id}/inventario/`)
}

export function obtenerRiesgoPorZona(id: number): Promise<RiesgoPorZona> {
  return api.get<RiesgoPorZona>(`/partidas/${id}/riesgo-por-zona/`)
}

/** Decisiones inseguras, con la alternativa que se le escapo al menor. */
export function obtenerOportunidadesMejora(
  id: number,
): Promise<OportunidadesMejora> {
  return api.get<OportunidadesMejora>(`/partidas/${id}/oportunidades-mejora/`)
}

export function listarNivelesRiesgo(): Promise<NivelRiesgo[]> {
  return api.get<NivelRiesgo[]>('/niveles-riesgo/')
}
