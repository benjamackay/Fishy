import type { Grupo, GrupoDetalle, NuevoGrupo, InvitacionGrupo, NuevaInvitacion, FamiliaEncontrada } from './grupos'
import type { NinoResumen, ReporteGrupo, ReporteNino } from './reportes'

export interface Consulta { signal?: AbortSignal }
/** Contrato de frontend. La autorización y las mutaciones reales pertenecen al backend. */
export interface FuentePanel {
  listarNinos(opciones?: Consulta): Promise<NinoResumen[]>
  obtenerReporteNino(id: number, opciones?: Consulta): Promise<ReporteNino>
  listarGrupos(opciones?: Consulta): Promise<Grupo[]>
  crearGrupo(datos: NuevoGrupo): Promise<Grupo>
  obtenerGrupo(id: string, opciones?: Consulta): Promise<GrupoDetalle>
  buscarFamilia(id: string, email: string): Promise<FamiliaEncontrada>
  agregarNinos(id: string, email: string, jugadorIds: number[]): Promise<GrupoDetalle>
  invitarFamilia(id: string, datos: NuevaInvitacion): Promise<InvitacionGrupo>
  reenviarInvitacion(id: string, invitacionId: string): Promise<InvitacionGrupo>
  cancelarInvitacion(id: string, invitacionId: string): Promise<void>
  eliminarUsuario(id: string, miembroId: string): Promise<void>
  eliminarGrupo(id: string): Promise<void>
  obtenerReporteGrupo(id: string, opciones?: Consulta): Promise<ReporteGrupo>
}
