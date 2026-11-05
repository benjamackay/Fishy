import type { AdultoResponsable } from '@/types/api'

export const perfilesDemo: Record<'principal' | 'alternativa', AdultoResponsable> = {
  principal: { id: 1001, nombre: 'Camila', apellido: 'Rojas', email: 'camila@example.com', edad: null, fecha_nacimiento: null, fecha_creacion: '2026-09-01T12:00:00Z', rol: 'padre' },
  alternativa: { id: 1002, nombre: 'Diego', apellido: 'Silva', email: 'diego@example.com', edad: null, fecha_nacimiento: null, fecha_creacion: '2026-09-01T12:00:00Z', rol: 'profesor' },
}
