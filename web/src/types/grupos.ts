export interface Grupo {
  id: string
  nombre: string
  descripcion: string
  total_miembros: number
  fecha_creacion: string
}
export interface NuevoGrupo {
  nombre: string
  descripcion?: string
}
/** Cada integrante representa a un niño; un padre puede recibir invitaciones para distintos hijos. */
export interface MiembroGrupo {
  id: string
  email: string
  nombre_nino: string
  fecha_ingreso: string
}
export interface GrupoDetalle extends Grupo {
  miembros: MiembroGrupo[]
  invitaciones: InvitacionGrupo[]
}
export interface NuevaInvitacion { email: string; nombre_nino: string }
export interface InvitacionGrupo extends NuevaInvitacion {
  id: string
  estado: 'pendiente' | 'aceptada' | 'cancelada' | 'vencida'
  estado_envio: 'enviando' | 'enviado' | 'fallido' | 'simulado'
  fecha_creacion: string
  vence_en: string
  enviada_en: string | null
}
export interface InvitacionPublica extends NuevaInvitacion { grupo: string; profesor: string; vence_en: string }
export interface AceptarInvitacion { token: string; confirmar: boolean; jugador_id?: number; crear_perfil?: boolean }
export interface InvitacionAceptada { jugador_id: number; grupo: string; aceptada: true }
