import { expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ContextoSesion } from '@/auth/contexto'
import { ContextoPanelDemo } from '@/hooks/usePanel'
import { perfilesDemo } from '@/demo/sesionDemo'
import { crearPanelPeriodo } from '@/demo/reportesPeriodo'
import { rutas } from '@/routes'

function abrir(sinAlertas = false) {
  const perfil = perfilesDemo.alternativa
  const base = crearPanelPeriodo(perfil.id)
  const fuente = { ...base, obtenerGrupo: async (id: string) => {
    const grupo = await base.obtenerGrupo(id)
    return sinAlertas ? { ...grupo, miembros: grupo.miembros.map(m => ({ ...m, areas_apoyo: [] })) } : grupo
  } }
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
  const user = abrir(true)
  await user.click(await screen.findByRole('button', { name: 'Solo quienes necesitan apoyo' }))
  expect(screen.getByRole('status').textContent).toContain('Sin alertas de apoyo')
  expect(screen.queryByText('Sofía')).toBeNull()
  await user.click(screen.getByRole('button', { name: 'Ver todos' }))
  expect(screen.getByText('Sofía')).toBeTruthy()
})
