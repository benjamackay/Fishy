import { expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'
import { prepararCuenta } from './fixtures/sesion'

function abrir() {
  prepararCuenta('principal')
  const router = createMemoryRouter(rutas, { initialEntries: ['/configuracion'] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return userEvent.setup()
}

it('abre configuración desde el menú y permite cerrarlo con Escape', async () => {
  const user = abrir()
  const boton = await screen.findByRole('button', { name: 'Menú de usuario' })
  await user.click(boton)
  expect(screen.getByRole('link', { name: 'Configuración' })).toBeTruthy()
  expect(screen.getByRole('button', { name: 'Cerrar sesión' })).toBeTruthy()
  await user.keyboard('{Escape}')
  expect(boton.getAttribute('aria-expanded')).toBe('false')
  expect(document.activeElement).toBe(boton)
  await user.click(boton)
  await user.click(screen.getByRole('link', { name: 'Configuración' }))
  expect(await screen.findByRole('form', { name: 'Información personal' })).toBeTruthy()
})

it('valida el perfil sin simular un guardado en la cuenta', async () => {
  const user = abrir()
  const nombre = await screen.findByRole('textbox', { name: /Nombre de usuario/ })
  await user.clear(nombre)
  await user.type(nombre, 'nuevo_nombre')
  await user.click(screen.getByRole('button', { name: 'Revisar cambios' }))
  expect(screen.getByRole('status').textContent).toContain('no aplica cambios')
  await user.click(screen.getByRole('button', { name: 'Descartar cambios' }))
  expect(screen.queryByRole('status')).toBeNull()
})

it('detecta contraseñas distintas y cierra la sesión desde seguridad', async () => {
  const user = abrir()
  await user.click(await screen.findByRole('button', { name: 'Contraseña y seguridad' }))
  await user.type(screen.getByLabelText('Contraseña actual', { exact: true }), 'Anterior123')
  await user.type(screen.getByLabelText('Nueva contraseña', { exact: true }), 'NuevaClave123')
  await user.type(screen.getByLabelText('Confirmar contraseña', { exact: true }), 'OtraClave123')
  await user.click(screen.getByRole('button', { name: 'Revisar contraseña' }))
  expect(screen.getByRole('alert').textContent).toContain('no coinciden')
  await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
  expect(await screen.findByRole('button', { name: 'Iniciar sesión', exact: true })).toBeTruthy()
  expect(localStorage.getItem('fishy.token')).toBeNull()
})
