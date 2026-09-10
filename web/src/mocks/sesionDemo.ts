import type { AdultoResponsable } from '@/types/api'

export const CLAVE_SESION_DEMO = 'fishy.demo.sesion.v2'
export const perfilesDemo: Record<'principal' | 'alternativa', AdultoResponsable> = {
  principal: { id: 1001, nombre: 'Camila', apellido: 'Rojas', email: 'camila@example.com', edad: null, fecha_nacimiento: null, fecha_creacion: '2026-09-01T12:00:00Z', is_admin: false },
  alternativa: { id: 1002, nombre: 'Diego', apellido: 'Silva', email: 'diego@example.com', edad: null, fecha_nacimiento: null, fecha_creacion: '2026-09-01T12:00:00Z', is_admin: true },
}
export function recuperarDemo(): AdultoResponsable | null {
  try {
    const cuenta = sessionStorage.getItem(CLAVE_SESION_DEMO)
    return cuenta === 'principal' || cuenta === 'alternativa' ? perfilesDemo[cuenta] : null
  } catch { return null }
}
