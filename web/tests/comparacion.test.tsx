import { expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ContextoSesion } from '@/auth/contexto'
import { ContextoPanelDemo } from '@/hooks/usePanel'
import { perfilesDemo } from '@/demo/sesionDemo'
import { crearPanelPeriodo } from '@/demo/reportesPeriodo'
import { ErrorUsuario } from '@/lib/errores'
import { rutas } from '@/routes'

function abrir(id = 'grupo-demo-1002-a', profesor = true, fuente = crearPanelPeriodo(1002)) {
  const perfil = perfilesDemo[profesor ? 'alternativa' : 'principal']
  const router = createMemoryRouter(rutas, { initialEntries: ['/admin/grupos/' + id + '/comparar'] })
  render(<ContextoSesion.Provider value={{ perfil, autenticado: true, cargando: false, esProfesor: profesor, entrar: async () => {}, salir: () => {} }}><ContextoPanelDemo.Provider value={fuente}><RouterProvider router={router} /></ContextoPanelDemo.Provider></ContextoSesion.Provider>)
  return userEvent.setup()
}
async function compararEjemplo(user: ReturnType<typeof userEvent.setup>, parcial = false) {
  await user.click(await screen.findByRole('button', { name: parcial ? 'Una temática sin muestra suficiente' : 'Dos muestras suficientes' }))
  await user.click(screen.getByRole('button', { name: 'Comparar períodos' }))
  return screen.findByRole('region', { name: 'Resultados de la comparación' })
}

it('muestra porcentajes, fechas, decisiones evaluadas y diferencia en puntos porcentuales', async () => {
  const user = abrir()
  const resultados = await compararEjemplo(user)
  expect(within(resultados).getAllByText('+100')).toHaveLength(3)
  expect(within(resultados).getAllByText('Del 05/10/2026 al 05/10/2026')).toHaveLength(3)
  expect(within(resultados).getAllByText('Del 15/10/2026 al 15/10/2026')).toHaveLength(3)
  expect(within(resultados).getAllByText(/4 decisiones evaluadas/)).toHaveLength(6)
  expect(within(resultados).queryByText('Sofía')).toBeNull()
})

it('suprime la diferencia de una temática con muestra insuficiente y conserva las otras', async () => {
  const user = abrir()
  await compararEjemplo(user, true)
  const ciberacoso = screen.getByRole('article', { name: 'Ciberacoso' })
  expect(within(ciberacoso).getByText(/Muestra insuficiente: se requieren al menos 3/)).toBeTruthy()
  expect(within(ciberacoso).getByText('Comparación no disponible')).toBeTruthy()
  expect(ciberacoso.querySelector('.difference-number')).toBeNull()
  expect(screen.getAllByText('-33,3')).toHaveLength(2)
})

it('rechaza fechas faltantes e invertidas sin solicitar agregados ni mostrar la comparación anterior', async () => {
  const fuente = crearPanelPeriodo(1002)
  const consultar = vi.spyOn(fuente, 'obtenerReporteGrupo')
  const user = abrir('grupo-demo-1002-a', true, fuente)
  await user.click(await screen.findByRole('button', { name: 'Comparar períodos' }))
  expect(screen.getByRole('alert').textContent).toContain('Selecciona la fecha inicial')
  expect(consultar).not.toHaveBeenCalled()
  await compararEjemplo(user)
  fireEvent.change(screen.getByLabelText('Fecha inicial · A'), { target: { value: '2026-10-20' } })
  await user.click(screen.getByRole('button', { name: 'Comparar períodos' }))
  expect(screen.getByRole('alert').textContent).toContain('posterior')
  expect(screen.queryByRole('region', { name: 'Resultados de la comparación' })).toBeNull()
  expect(consultar).toHaveBeenCalledTimes(2)
})

it('deniega un grupo ajeno sin cargar sus métricas', async () => {
  const fuente = crearPanelPeriodo(1002)
  const consultar = vi.spyOn(fuente, 'obtenerReporteGrupo')
  abrir('grupo-demo-1001-a', true, fuente)
  expect((await screen.findByRole('alert')).textContent).toContain('No encontramos este grupo en tu cuenta')
  expect(consultar).not.toHaveBeenCalled()
  expect(screen.queryByRole('button', { name: 'Comparar períodos' })).toBeNull()
  expect(screen.queryByRole('region', { name: 'Resultados de la comparación' })).toBeNull()
})

it('vuelve a comprobar pertenencia al comparar y no publica resultados si se revoca el acceso', async () => {
  const fuente = crearPanelPeriodo(1002)
  const detalle = await fuente.obtenerGrupo('grupo-demo-1002-a')
  vi.spyOn(fuente, 'obtenerGrupo').mockResolvedValueOnce(detalle).mockRejectedValue(new ErrorUsuario('Acceso denegado.'))
  const consultar = vi.spyOn(fuente, 'obtenerReporteGrupo')
  const user = abrir('grupo-demo-1002-a', true, fuente)
  await user.click(await screen.findByRole('button', { name: 'Dos muestras suficientes' }))
  await user.click(screen.getByRole('button', { name: 'Comparar períodos' }))
  await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('Acceso denegado'))
  expect(consultar).not.toHaveBeenCalled()
  expect(screen.queryByRole('region', { name: 'Resultados de la comparación' })).toBeNull()
})

it('deniega la comparación a una familia aunque conozca la dirección del grupo', async () => {
  abrir('grupo-demo-1002-a', false)
  expect((await screen.findByRole('alert')).textContent).toContain('Necesitas una cuenta de profesor')
  expect(screen.queryByRole('button', { name: 'Comparar períodos' })).toBeNull()
})
