import type { Grupo, GrupoDetalle, NuevoGrupo } from './grupos'
import type { NinoResumen, ReporteGrupo, ReporteNino } from './reportes'

export interface Consulta { signal?: AbortSignal }
/** Contrato de frontend. La autorización y las mutaciones reales pertenecen al backend. */
export interface FuentePanel {
  listarNinos(opciones?: Consulta): Promise<NinoResumen[]>
  obtenerReporteNino(id: number, opciones?: Consulta): Promise<ReporteNino>
  listarGrupos(opciones?: Consulta): Promise<Grupo[]>
  crearGrupo(datos: NuevoGrupo): Promise<Grupo>
  obtenerGrupo(id: string, opciones?: Consulta): Promise<GrupoDetalle>
  agregarUsuario(id: string, email: string): Promise<GrupoDetalle>
  eliminarUsuario(id: string, miembroId: string): Promise<void>
  eliminarGrupo(id: string): Promise<void>
  obtenerReporteGrupo(id: string, opciones?: Consulta): Promise<ReporteGrupo>
}
