import { ErrorUsuario } from './errores'
import type { AdultoResponsable } from '@/types/api'
import type { FuentePanel } from '@/types/panel'

/** Defensa de frontend: el servidor debe verificar también rol y pertenencia.
 * Se comprueba el permiso antes de leer datos individuales o de grupos.
 */
export function aplicarPermisosPanel(fuente: FuentePanel, perfil: AdultoResponsable | null): FuentePanel {
  function comprobarPadre(usuario: AdultoResponsable | null): asserts usuario is AdultoResponsable {
    if (!usuario) throw new ErrorUsuario('Inicia sesión para consultar tus reportes.')
    if (usuario.rol === 'profesor') throw new ErrorUsuario('Los tutores administradores solo tienen acceso a grupos y reportes grupales.')
    if (usuario.rol !== 'padre') throw new ErrorUsuario('Esta cuenta no tiene acceso a reportes individuales.')
  }
  async function soloAdministrador<T>(operacion: () => Promise<T>): Promise<T> {
    if (perfil?.rol !== 'profesor') throw new ErrorUsuario('Solo los tutores administradores pueden gestionar grupos y consultar sus reportes.')
    return operacion()
  }
  return {
    listarNinos: async opciones => {
      comprobarPadre(perfil)
      return (await fuente.listarNinos(opciones)).filter(n => n.adulto_id === perfil.id)
    },
    obtenerReporteNino: async (id, opciones) => {
      comprobarPadre(perfil)
      const reporte = await fuente.obtenerReporteNino(id, opciones)
      if (reporte.nino.id !== id || reporte.nino.adulto_id !== perfil.id) throw new ErrorUsuario('No tienes acceso a este reporte.')
      return reporte
    },
    listarGrupos: opciones => soloAdministrador(() => fuente.listarGrupos(opciones)),
    crearGrupo: datos => soloAdministrador(() => fuente.crearGrupo(datos)),
    obtenerGrupo: (id, opciones) => soloAdministrador(() => fuente.obtenerGrupo(id, opciones)),
    buscarFamilia: (id, email) => soloAdministrador(() => fuente.buscarFamilia(id, email)),
    agregarNinos: (id, email, jugadorIds) => soloAdministrador(() => fuente.agregarNinos(id, email, jugadorIds)),
    invitarFamilia: (id, datos) => soloAdministrador(() => fuente.invitarFamilia(id, datos)),
    reenviarInvitacion: (id, invitacionId) => soloAdministrador(() => fuente.reenviarInvitacion(id, invitacionId)),
    cancelarInvitacion: (id, invitacionId) => soloAdministrador(() => fuente.cancelarInvitacion(id, invitacionId)),
    eliminarUsuario: (id, miembroId) => soloAdministrador(() => fuente.eliminarUsuario(id, miembroId)),
    eliminarGrupo: id => soloAdministrador(() => fuente.eliminarGrupo(id)),
    obtenerReporteGrupo: (id, opciones) => soloAdministrador(() => fuente.obtenerReporteGrupo(id, opciones)),
  }
}
