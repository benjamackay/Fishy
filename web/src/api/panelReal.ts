import { listarJugadores } from '@/api/jugadores'
import { ErrorUsuario } from '@/lib/errores'
import type { FuentePanel } from '@/types/panel'

/**
 * Punto de conexión para el integrante a cargo de endpoints.
 * No se inventan rutas ni se transforman puntajes de riesgo en decisiones seguras.
 * Sustituir las operaciones pendientes por llamadas a la API acordada.
 * El login y el listado de perfiles existentes se conservan.
 */
const pendiente = async (): Promise<never> => {
  throw new ErrorUsuario('Esta función aún no está disponible. Inténtalo más adelante.')
}
export const panelReal: FuentePanel = {
  listarNinos: async () => (await listarJugadores()).map(j => ({
    id: j.id, adulto_id: j.adulto, nombre: j.nombre, edad: j.edad, actualizado_en: null,
  })),
  obtenerReporteNino: pendiente,
  listarGrupos: pendiente,
  crearGrupo: pendiente,
  obtenerGrupo: pendiente,
  agregarUsuario: pendiente,
  eliminarUsuario: pendiente,
  eliminarGrupo: pendiente,
  obtenerReporteGrupo: pendiente,
}
