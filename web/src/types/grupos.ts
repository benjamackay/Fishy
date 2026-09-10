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
/** El correo identifica al usuario registrado; el backend resuelve su identidad. */
export interface MiembroGrupo {
  id: string
  email: string
  fecha_ingreso: string
}
export interface GrupoDetalle extends Grupo {
  miembros: MiembroGrupo[]
}
