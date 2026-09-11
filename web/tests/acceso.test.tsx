import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'

function abrir() {
  const router = createMemoryRouter(rutas, { initialEntries: ['/login'] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return userEvent.setup()
}

async function completarRegistro(user: ReturnType<typeof userEvent.setup>, confirmacion = 'Clave-larga-24') {
  await user.click(screen.getByRole('tab', { name: 'Registrarse' }))
  await user.type(screen.getByLabelText('Nombre de usuario'), 'nuevo_tutor')
  await user.type(screen.getByLabelText('Correo electrónico'), 'familia@example.com')
  await user.type(screen.getByLabelText('Contraseña', { exact: true }), 'Clave-larga-24')
  await user.type(screen.getByLabelText('Confirmar contraseña'), confirmacion)
}

describe('acceso y registro', () => {
  it('permite alternar con teclado y solo expone el formulario activo', async () => {
    const user = abrir()
    const login = screen.getByRole('tab', { name: 'Iniciar sesión' })
    const registro = screen.getByRole('tab', { name: 'Registrarse' })
    expect(login.getAttribute('aria-selected')).toBe('true')
    expect(screen.queryByLabelText('Correo electrónico')).toBeNull()
    login.focus()
    await user.keyboard('{ArrowRight}')
    expect(document.activeElement).toBe(registro)
    expect(registro.getAttribute('aria-selected')).toBe('true')
    expect(screen.getByRole('tabpanel').id).toBe('panel-registro')
    expect(screen.getByLabelText('Correo electrónico')).toBeTruthy()
    expect(screen.queryByRole('form', { name: 'Iniciar sesión' })).toBeNull()
    await user.keyboard('{Home}')
    expect(document.activeElement).toBe(login)
    expect(screen.getByRole('tabpanel').id).toBe('panel-login')
    expect(screen.queryByLabelText('Correo electrónico')).toBeNull()
  })
  it('rechaza contraseñas diferentes antes de enviar datos y enfoca la confirmación', async () => {
    const enviar = vi.fn()
    vi.stubGlobal('fetch', enviar)
    const user = abrir()
    await completarRegistro(user, 'Otra-clave-24')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta', exact: true }))
    expect(enviar).not.toHaveBeenCalled()
    const confirmacion = screen.getByLabelText('Confirmar contraseña')
    expect(confirmacion.getAttribute('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(confirmacion)
    expect(screen.getByText('Las contraseñas deben coincidir.')).toBeTruthy()
  })
  it('envía el contrato existente y confirma la cuenta sin iniciar sesión ni guardar contraseñas', async () => {
    const enviar = vi.fn().mockResolvedValue(new Response(JSON.stringify({ token: 'token-registro', adulto_id: 77 }), { status: 201 }))
    vi.stubGlobal('fetch', enviar)
    const user = abrir()
    await completarRegistro(user)
    await user.type(screen.getByLabelText(/^Apellido/), 'Rojas')
    await user.type(screen.getByLabelText(/^Fecha de nacimiento/), '1990-05-17')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta', exact: true }))
    await screen.findByRole('heading', { name: 'Tu cuenta está lista.' })
    expect(enviar).toHaveBeenCalledTimes(1)
    const [url, solicitud] = enviar.mock.calls[0]
    expect(url).toMatch(/\/auth\/registro\/$/)
    expect(solicitud.method).toBe('POST')
    expect(JSON.parse(solicitud.body)).toEqual({ nombre: 'nuevo_tutor', email: 'familia@example.com', password: 'Clave-larga-24', apellido: 'Rojas', fecha_nacimiento: '1990-05-17' })
    expect(localStorage.getItem('fishy.token')).toBeNull()
    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
    await user.click(screen.getByRole('button', { name: 'Ir a iniciar sesión' }))
    expect((screen.getByLabelText('Nombre de usuario') as HTMLInputElement).value).toBe('nuevo_tutor')
    expect((screen.getByLabelText('Contraseña', { exact: true }) as HTMLInputElement).value).toBe('')
    expect(screen.queryByRole('link', { name: 'Mis grupos' })).toBeNull()
  })
  it('un rechazo del servicio conserva el formulario sin simular una cuenta exitosa', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ email: ['Ya existe.'] }), { status: 400 })))
    const user = abrir()
    await completarRegistro(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta', exact: true }))
    expect((await screen.findByRole('alert')).textContent).toContain('podrían estar registrados')
    expect(screen.queryByRole('heading', { name: 'Tu cuenta está lista.' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeTruthy()
    expect(localStorage.getItem('fishy.token')).toBeNull()
    expect(sessionStorage.getItem('fishy.demo.sesion.v2')).toBeNull()
  })
  it('no confirma una respuesta incompleta del servicio', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 201 })))
    const user = abrir()
    await completarRegistro(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta', exact: true }))
    expect((await screen.findByRole('alert')).textContent).toContain('No pudimos confirmar el registro')
    expect(screen.queryByRole('heading', { name: 'Tu cuenta está lista.' })).toBeNull()
    expect(localStorage.getItem('fishy.token')).toBeNull()
  })
  it('permite mostrar la contraseña y vuelve a ocultarla al cambiar de vista', async () => {
    const user = abrir()
    const clave = screen.getByLabelText('Contraseña', { exact: true }) as HTMLInputElement
    await user.type(clave, 'Clave-privada')
    expect(clave.type).toBe('password')
    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña', exact: true }))
    expect(clave.type).toBe('text')
    expect(clave.value).toBe('Clave-privada')
    await user.click(screen.getByRole('tab', { name: 'Registrarse' }))
    await user.click(screen.getByRole('tab', { name: 'Iniciar sesión' }))
    const nueva = screen.getByLabelText('Contraseña', { exact: true }) as HTMLInputElement
    expect(nueva.type).toBe('password')
    expect(nueva.value).toBe('')
  })
})
