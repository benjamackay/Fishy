import { api } from '@/lib/api'
import type { Partida, UsuarioJugador } from '@/types/api'

/** Perfiles de menores del adulto autenticado. */
export function listarJugadores(): Promise<UsuarioJugador[]> {
  return api.get<UsuarioJugador[]>('/jugadores/')
}

export function obtenerJugador(id: number): Promise<UsuarioJugador> {
  return api.get<UsuarioJugador>(`/jugadores/${id}/`)
}

export function listarPartidasDeJugador(id: number): Promise<Partida[]> {
  return api.get<Partida[]>(`/jugadores/${id}/partidas/`)
}
