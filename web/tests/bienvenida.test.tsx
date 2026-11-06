import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'

function abrir(ruta = '/') {
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return { user: userEvent.setup(), router }
}

it('recibe visitantes con información y contacto sin consultar el backend', () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  abrir()
  expect(screen.getByRole('heading', { name: /Un mundo digital/ })).toBeTruthy()
  expect(screen.getAllByRole('link', { name: 'Datos y privacidad', exact: true })[0].getAttribute('href')).toBe('/#datos')
  expect(screen.getByRole('link', { name: /contacto@fishygame.cl/ }).getAttribute('href')).toBe('mailto:contacto@fishygame.cl')
  expect(screen.queryByRole('form')).toBeNull()
  expect(fetch).not.toHaveBeenCalled()
})

it('abre un formulario independiente según el acceso elegido', async () => {
  const { user, router } = abrir()
  await user.click(screen.getByRole('link', { name: 'Registrarse', exact: true }))
  expect(router.state.location.pathname).toBe('/registro')
  expect(screen.getByRole('form', { name: 'Crear una cuenta' })).toBeTruthy()
  expect(screen.queryByRole('form', { name: 'Iniciar sesión' })).toBeNull()
  await user.click(screen.getAllByRole('link', { name: 'Iniciar sesión', exact: true })[0])
  expect(router.state.location.pathname).toBe('/login')
  expect(screen.getByRole('form', { name: 'Iniciar sesión' })).toBeTruthy()
})

it('permite explorar las temáticas', async () => {
  const { user } = abrir()
  await user.click(screen.getByRole('button', { name: 'Retos virales' }))
  expect(screen.getByText('Pensar antes de seguir un reto. Elegir también es cuidarse.')).toBeTruthy()
})

it('pide una sesión para acceder al panel', async () => {
  const { router } = abrir('/panel')
  expect(await screen.findByRole('form', { name: 'Iniciar sesión' })).toBeTruthy()
  expect(router.state.location.pathname).toBe('/login')
})
