import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorDemo } from '@/demo/ProveedorDemo'
import { rutas } from '@/routes'

it('permite recorrer reportes y configuración sin token ni llamadas al backend', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const router = createMemoryRouter(rutas, { initialEntries: ['/'] })
  render(<ProveedorDemo><RouterProvider router={router} /></ProveedorDemo>)
  const user = userEvent.setup()
  expect(await screen.findByRole('heading', { name: 'Martina' })).toBeTruthy()
  await user.click(screen.getByRole('button', { name: 'Menú de usuario' }))
  await user.click(screen.getByRole('link', { name: 'Configuración' }))
  expect(await screen.findByRole('form', { name: 'Información personal' })).toBeTruthy()
  await user.click(screen.getByRole('button', { name: 'Menú de usuario' }))
  await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
  await user.click(await screen.findByRole('button', { name: 'Entrar con cuenta de muestra' }))
  expect(await screen.findByRole('form', { name: 'Información personal' })).toBeTruthy()
  expect(localStorage.getItem('fishy.token')).toBeNull()
  expect(fetch).not.toHaveBeenCalled()
})
