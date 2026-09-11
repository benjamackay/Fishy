import type { Grupo, GrupoDetalle, NuevoGrupo, InvitacionGrupo, NuevaInvitacion } from './grupos'
import type { NinoResumen, ReporteGrupo, ReporteNino } from './reportes'
import type { SeguimientoGrupo } from './seguimiento'

export interface Consulta { signal?: AbortSignal }
/** Contrato de frontend. La autorización y las mutaciones reales pertenecen al backend. */
export interface FuentePanel {
  listarNinos(opciones?: Consulta): Promise<NinoResumen[]>
  obtenerReporteNino(id: number, opciones?: Consulta): Promise<ReporteNino>
  listarGrupos(opciones?: Consulta): Promise<Grupo[]>
  crearGrupo(datos: NuevoGrupo): Promise<Grupo>
  obtenerGrupo(id: string, opciones?: Consulta): Promise<GrupoDetalle>
  invitarFamilia(id: string, datos: NuevaInvitacion): Promise<InvitacionGrupo>
  reenviarInvitacion(id: string, invitacionId: string): Promise<InvitacionGrupo>
  cancelarInvitacion(id: string, invitacionId: string): Promise<void>
  eliminarUsuario(id: string, miembroId: string): Promise<void>
  eliminarGrupo(id: string): Promise<void>
  obtenerReporteGrupo(id: string, opciones?: Consulta): Promise<ReporteGrupo>
  obtenerSeguimientoGrupo(id: string, opciones?: Consulta): Promise<SeguimientoGrupo>
}
