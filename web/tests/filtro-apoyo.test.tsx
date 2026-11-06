import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ContextoSesion } from '@/auth/contexto'
import { ContextoPanelDemo } from '@/hooks/usePanel'
import { perfilesDemo } from '@/demo/sesionDemo'
import { crearPanelPeriodo } from '@/demo/reportesPeriodo'
import { rutas } from '@/routes'
import { panelReal } from '@/api/panelReal'
import { api } from '@/lib/api'
import type { FuentePanel } from '@/types/panel'

function abrir(obtenerSeguimiento?: FuentePanel['obtenerSeguimiento']) {
  const perfil = perfilesDemo.alternativa
  const base = crearPanelPeriodo(perfil.id)
  const fuente = { ...base, obtenerSeguimiento: obtenerSeguimiento ?? base.obtenerSeguimiento }
  const router = createMemoryRouter(rutas, { initialEntries: ['/admin/grupos/grupo-demo-1002-a'] })
  render(<ContextoSesion.Provider value={{ perfil, autenticado: true, cargando: false, esProfesor: true, entrar: async () => {}, salir: () => {} }}><ContextoPanelDemo.Provider value={fuente}><RouterProvider router={router} /></ContextoPanelDemo.Provider></ContextoSesion.Provider>)
  return userEvent.setup()
}

it('alterna directamente entre todos los integrantes y quienes necesitan apoyo', async () => {
  const user = abrir()
  const filtro = await screen.findByRole('button', { name: 'Solo quienes necesitan apoyo' })
  expect(screen.getByText('Agustín')).toBeTruthy()
  expect(filtro.getAttribute('aria-pressed')).toBe('false')
  await user.click(filtro)
  expect(filtro.getAttribute('aria-pressed')).toBe('true')
  expect(screen.queryByText('Agustín')).toBeNull()
  expect(screen.getByText('Sofía')).toBeTruthy()
  expect(screen.getByText('Lucas')).toBeTruthy()
  expect(screen.getByText('3 de 4 perfiles vinculados')).toBeTruthy()
  expect(screen.getByRole('link', { name: 'Ver reporte' })).toBeTruthy()
  await user.click(filtro)
  expect(screen.getByText('Agustín')).toBeTruthy()
})

it('permite volver a todos cuando no hay alertas que coincidan', async () => {
  const user = abrir(async () => ({ alumnos: [] }))
  await user.click(await screen.findByRole('button', { name: 'Solo quienes necesitan apoyo' }))
  expect(screen.getByRole('status').textContent).toContain('Sin alertas de apoyo')
  expect(screen.queryByText('Sofía')).toBeNull()
  await user.click(screen.getByRole('button', { name: 'Ver todos' }))
  expect(screen.getByText('Sofía')).toBeTruthy()
})

it('muestra solo las areas en alerta y deja a los prioritarios primero', async () => {
  const user = abrir(async () => ({ alumnos: [
    { miembro_id: 'usuario-904', estado: 'prioritario', tematicas: [{ tematica: 'ciberacoso', estado: 'prioritario' }, { tematica: 'desconocidos', estado: 'sin_alertas' }] },
    { miembro_id: 'usuario-103', estado: 'apoyo', tematicas: [{ tematica: 'retos_virales', estado: 'apoyo' }, { tematica: 'ciberacoso', estado: 'muestra_insuficiente' }] },
    { miembro_id: 'usuario-905', estado: 'muestra_insuficiente', tematicas: [{ tematica: 'desconocidos', estado: 'muestra_insuficiente' }] },
  ] }))
  await user.click(await screen.findByRole('button', { name: 'Solo quienes necesitan apoyo' }))
  expect(screen.getByText('2 de 4 perfiles vinculados')).toBeTruthy()
  expect(screen.getAllByRole('note').map(n => n.getAttribute('aria-label'))).toEqual(['Necesita apoyo en Ciberacoso', 'Necesita apoyo en Retos Virales'])
  expect(screen.queryByText('Emilia')).toBeNull()
})

it('si falla el seguimiento lo dice y no lo presenta como sin alertas', async () => {
  abrir(async () => { throw new Error('caída') })
  expect((await screen.findByRole('alert')).textContent).toContain('Alertas no disponibles')
  expect(screen.getByText('Agustín')).toBeTruthy()
  expect((screen.getByRole('button', { name: 'Solo quienes necesitan apoyo' }) as HTMLButtonElement).disabled).toBe(true)
  expect(screen.queryByText('0')).toBeNull()
})

it('consulta el endpoint de seguimiento del grupo', async () => {
  const get = vi.spyOn(api, 'get').mockResolvedValue({ alumnos: [] })
  await panelReal.obtenerSeguimiento('abc')
  expect(get.mock.calls[0][0]).toBe('/grupos/abc/seguimiento/')
  get.mockRestore()
})
