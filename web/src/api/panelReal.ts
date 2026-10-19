import { listarJugadores } from '@/api/jugadores'
import { api, ApiError } from '@/lib/api'
import { ErrorUsuario } from '@/lib/errores'
import { operacionGrupo } from './invitaciones'
import type { FuentePanel } from '@/types/panel'
import type { Grupo, GrupoDetalle, InvitacionGrupo, FamiliaEncontrada } from '@/types/grupos'
import type { NinoResumen, ReporteGrupo, ReporteNino } from '@/types/reportes'

/**
 * Grupos e invitaciones usan el contrato implementado en Backend/backend/api/invitaciones.py.
 * Los reportes se agregan en el servidor a partir de los perfiles infantiles aceptados.
 * Invitar responde 503 mientras el backend no tenga configurado el envío de correo.
 */
const ruta = (id: string) => '/grupos/' + encodeURIComponent(id) + '/'
export const panelReal: FuentePanel = {
  listarNinos: async opciones => {
    const jugadores = await listarJugadores(opciones)
    const resumenes: NinoResumen[] = jugadores.map(j => ({
      id: j.id, adulto_id: j.adulto, nombre: j.nombre, edad: j.edad, actualizado_en: null,
    }))
    let siguiente = 0
    // El listado no trae métricas. Consultarlas sin saturar la API con todas a la vez.
    await Promise.all(Array.from({ length: Math.min(3, jugadores.length) }, async () => {
      while (siguiente < jugadores.length) {
        opciones?.signal?.throwIfAborted()
        const indice = siguiente++
        const nino = resumenes[indice]
        try {
          const reporte = await api.get<ReporteNino>('/jugadores/' + nino.id + '/reporte/', opciones)
          if (reporte.nino.id !== nino.id || reporte.nino.adulto_id !== nino.adulto_id) {
            throw new ErrorUsuario('No pudimos verificar a quién pertenece este reporte.')
          }
          resumenes[indice] = { ...nino, actualizado_en: reporte.actualizado_en, tematicas: reporte.tematicas }
        } catch (error) {
          opciones?.signal?.throwIfAborted()
          if (error instanceof ErrorUsuario || (error instanceof ApiError && [401, 403].includes(error.status))) throw error
          // Sin resumen no significa sin actividad: la pantalla lo indica como no disponible.
        }
      }
    }))
    return resumenes
  },
  obtenerReporteNino: (id, opciones) => api.get<ReporteNino>('/jugadores/' + id + '/reporte/', opciones),
  listarGrupos: opciones => operacionGrupo(() => api.get<Grupo[]>('/grupos/', opciones)),
  crearGrupo: datos => operacionGrupo(() => api.post<Grupo>('/grupos/', datos)),
  obtenerGrupo: (id, opciones) => operacionGrupo(() => api.get<GrupoDetalle>(ruta(id), opciones)),
  buscarFamilia: (id, email) => operacionGrupo(() => api.post<FamiliaEncontrada>(ruta(id) + 'buscar-familia/', { email })),
  agregarNinos: (id, email, jugadorIds) => operacionGrupo(() => api.post<GrupoDetalle>(ruta(id) + 'miembros/', { email, jugador_ids: jugadorIds })),
  invitarFamilia: (id, datos) => operacionGrupo(() => api.post<InvitacionGrupo>(ruta(id) + 'invitaciones/', datos)),
  reenviarInvitacion: (id, invitacionId) => operacionGrupo(() => api.post<InvitacionGrupo>(ruta(id) + 'invitaciones/' + encodeURIComponent(invitacionId) + '/')),
  cancelarInvitacion: (id, invitacionId) => operacionGrupo(() => api.delete<void>(ruta(id) + 'invitaciones/' + encodeURIComponent(invitacionId) + '/')),
  eliminarUsuario: (id, miembroId) => operacionGrupo(() => api.delete<void>(ruta(id) + 'miembros/' + encodeURIComponent(miembroId) + '/')),
  eliminarGrupo: id => operacionGrupo(() => api.delete<void>(ruta(id))),
  obtenerReporteGrupo: (id, opciones) => operacionGrupo(() => api.get<ReporteGrupo>(ruta(id) + 'reporte/', opciones)),
}
