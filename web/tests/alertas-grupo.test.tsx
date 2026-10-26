import { expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AlertasGrupo } from '@/components/AlertasGrupo'
import { alertasGrupoEjemplo } from '@/demo/alertasGrupo'

it('identifica los ejemplos y no consulta servicios ni muestra porcentajes', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  render(<AlertasGrupo />)
  expect(screen.queryByRole('heading', { name: 'Sofía' })).toBeNull()
  const user = userEvent.setup()
  await user.click(screen.getByRole('button', { name: /Ver ejemplo de alertas/ }))
  expect(screen.getByText('Vista previa · niños y alertas ficticios')).toBeTruthy()
  const seccion = screen.getByRole('region', { name: /Quién necesita/ })
  expect(within(seccion).getByRole('heading', { name: 'Sofía' })).toBeTruthy()
  expect(seccion.textContent).not.toContain('%')
  expect(seccion.querySelectorAll('[role="meter"], progress')).toHaveLength(0)
  expect(fetch).not.toHaveBeenCalled()
  await user.click(screen.getByRole('button', { name: 'Cerrar ejemplo' }))
  expect(screen.queryByRole('heading', { name: 'Sofía' })).toBeNull()
})

it('filtra por área y prioridad sin mostrar otras alertas del mismo niño', async () => {
  render(<AlertasGrupo alertas={alertasGrupoEjemplo} />)
  const user = userEvent.setup()
  await user.selectOptions(screen.getByRole('combobox', { name: 'Área' }), 'desconocidos')
  await user.selectOptions(screen.getByRole('combobox', { name: 'Tipo de alerta' }), 'reforzar')
  expect(screen.getByRole('heading', { name: 'Emilia' })).toBeTruthy()
  expect(screen.queryByRole('heading', { name: 'Sofía' })).toBeNull()
  expect(screen.queryByRole('heading', { name: 'Lucas' })).toBeNull()
  await user.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
  expect(screen.getByRole('heading', { name: 'Sofía' })).toBeTruthy()
  expect(screen.getByRole('heading', { name: 'Lucas' })).toBeTruthy()
})

it('busca nombres sin tildes y ofrece recuperar los resultados vacíos', async () => {
  render(<AlertasGrupo alertas={alertasGrupoEjemplo} />)
  const user = userEvent.setup()
  await user.type(screen.getByRole('searchbox', { name: 'Buscar niño' }), 'sofia')
  expect(screen.getByRole('heading', { name: 'Sofía' })).toBeTruthy()
  expect(screen.queryByRole('heading', { name: 'Lucas' })).toBeNull()
  await user.clear(screen.getByRole('searchbox', { name: 'Buscar niño' }))
  await user.type(screen.getByRole('searchbox', { name: 'Buscar niño' }), 'inexistente')
  expect(screen.getByText('No hay coincidencias con estos filtros')).toBeTruthy()
  await user.click(screen.getByRole('button', { name: 'Ver todas las alertas' }))
  expect(screen.getByRole('heading', { name: 'Lucas' })).toBeTruthy()
})

it('expande sugerencias y distingue ausencia de alertas de ausencia de datos', async () => {
  const { unmount } = render(<AlertasGrupo alertas={alertasGrupoEjemplo} />)
  const user = userEvent.setup()
  const detalle = screen.getByText('Necesita apoyo para identificar cómo actuar cuando alguien recibe mensajes que le hacen daño.').closest('details')!
  expect(detalle.open).toBe(false)
  await user.click(detalle.querySelector('summary')!)
  expect(detalle.open).toBe(true)
  expect(within(detalle).getByText('Cómo acompañar')).toBeTruthy()
  unmount()
  render(<AlertasGrupo alertas={[]} />)
  expect(screen.getByText('No hay alertas para mostrar')).toBeTruthy()
  expect(screen.getByText(/La ausencia de alertas no equivale/)).toBeTruthy()
})
