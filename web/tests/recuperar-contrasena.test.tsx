import { beforeEach, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ solicitud_id: 'solicitud-prueba', reenvio_en_segundos: 60, expira_en_segundos: 600 }), { status: 202 })))
})

function abrir(ruta = '/recuperar-contrasena') {
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return { user: userEvent.setup(), router }
}

it('abre la recuperación desde el login y permite volver', async () => {
  const { user, router } = abrir('/login')
  await user.click(screen.getByRole('link', { name: '¿Olvidaste tu contraseña?' }))
  expect(router.state.location.pathname).toBe('/recuperar-contrasena')
  expect(screen.getByRole('form', { name: 'Recuperar contraseña' })).toBeTruthy()
  await user.click(screen.getByRole('link', { name: 'Volver a iniciar sesión' }))
  expect(screen.getByRole('form', { name: 'Iniciar sesión' })).toBeTruthy()
})

it('rechaza correos vacíos y con formato inválido', async () => {
  const { user } = abrir()
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  expect(screen.getByRole('alert').textContent).toContain('correo electrónico válido')
  const email = screen.getByLabelText('Correo electrónico')
  expect(document.activeElement).toBe(email)
  await user.type(email, 'correo-invalido')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  expect(email.getAttribute('aria-invalid')).toBe('true')
  expect(screen.queryByRole('heading', { name: 'Correo validado' })).toBeNull()
})

it('solicita un código al servidor y permite corregir el correo', async () => {
  const { user } = abrir()
  await user.type(screen.getByLabelText('Correo electrónico'), 'familia@ejemplo.cl')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  expect(await screen.findByRole('heading', { name: 'Ingresa tu código' })).toBeTruthy()
  expect(document.activeElement).toBe(screen.getByLabelText('Dígito 1 de 6'))
  expect(screen.getByText('familia@ejemplo.cl')).toBeTruthy()
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/auth/recuperacion/solicitar/'), expect.objectContaining({ body: JSON.stringify({ email: 'familia@ejemplo.cl' }) }))
  expect(localStorage.length).toBe(0)
  expect(sessionStorage.length).toBe(0)
  await user.click(screen.getByRole('button', { name: 'Usar otro correo' }))
  expect(screen.getByRole('form', { name: 'Recuperar contraseña' })).toBeTruthy()
})

it('admite solo números, avanza entre casillas y permite pegar el código completo', async () => {
  const { user } = abrir()
  await user.type(screen.getByLabelText('Correo electrónico'), 'familia@ejemplo.cl')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  const primero = await screen.findByLabelText('Dígito 1 de 6') as HTMLInputElement
  await user.keyboard('a')
  expect(primero.value).toBe('')
  await user.keyboard('12')
  expect(document.activeElement).toBe(screen.getByLabelText('Dígito 3 de 6'))
  await user.keyboard('{Backspace}')
  expect(document.activeElement).toBe(screen.getByLabelText('Dígito 2 de 6'))
  expect((screen.getByLabelText('Dígito 2 de 6') as HTMLInputElement).value).toBe('')
  await user.click(primero)
  await user.paste('123456')
  for (let i = 0; i < 6; i++) expect((screen.getByLabelText('Dígito ' + (i + 1) + ' de 6') as HTMLInputElement).value).toBe(String(i + 1))
  vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ reset_token: 'permiso-prueba' })))
  await user.click(screen.getByRole('button', { name: 'Verificar código' }))
  expect(await screen.findByRole('form', { name: 'Restablecer contraseña' })).toBeTruthy()
  expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/auth/recuperacion/verificar/'), expect.objectContaining({ body: JSON.stringify({ solicitud_id: 'solicitud-prueba', codigo: '123456' }) }))
  await user.type(screen.getByLabelText('Nueva contraseña', { exact: true }), 'ClaveNueva-9482')
  await user.type(screen.getByLabelText('Confirmar contraseña', { exact: true }), 'ClaveNueva-9482')
  vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ requiere_login: true })))
  await user.click(screen.getByRole('button', { name: 'Guardar contraseña' }))
  expect(await screen.findByRole('heading', { name: 'Contraseña actualizada' })).toBeTruthy()
  expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/auth/recuperacion/restablecer/'), expect.objectContaining({ body: JSON.stringify({ reset_token: 'permiso-prueba', password_nueva: 'ClaveNueva-9482', password_confirmacion: 'ClaveNueva-9482' }) }))
})

it('no permite verificar un código incompleto', async () => {
  const { user } = abrir()
  await user.type(screen.getByLabelText('Correo electrónico'), 'familia@ejemplo.cl')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  await screen.findByLabelText('Dígito 1 de 6')
  await user.keyboard('12')
  await user.click(screen.getByRole('button', { name: 'Verificar código' }))
  expect(screen.getByRole('alert').textContent).toContain('Completa los 6 dígitos')
  expect(document.activeElement).toBe(screen.getByLabelText('Dígito 3 de 6'))
})
