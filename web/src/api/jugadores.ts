import { api } from '@/lib/api'
import type { Partida, UsuarioJugador } from '@/types/api'

/** Perfiles de menores del adulto autenticado. */
export function listarJugadores(opciones?: { signal?: AbortSignal }): Promise<UsuarioJugador[]> {
  return api.get<UsuarioJugador[]>('/jugadores/', opciones)
}

export function obtenerJugador(id: number): Promise<UsuarioJugador> {
  return api.get<UsuarioJugador>(`/jugadores/${id}/`)
}

export function listarPartidasDeJugador(id: number): Promise<Partida[]> {
  return api.get<Partida[]>(`/jugadores/${id}/partidas/`)
}
