import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'

const token = 'a'.repeat(43)
const invitacion = { grupo: '5° Básico A', profesor: 'Profesora Ana', nombre_nino: 'Martina', email: 'familia@example.com', vence_en: '2027-01-01T12:00:00Z' }
const padre = { id: 4, nombre: 'padre', email: invitacion.email, apellido: '', is_admin: false }
const perfiles = [
  { id: 10, adulto: 4, nombre: 'Martina', edad: 10 },
  { id: 11, adulto: 4, nombre: 'Tomás', edad: 7 },
  { id: 12, adulto: 8, nombre: 'Martina', edad: 6 },
]
function abrir({ sesion = true, email = padre.email, errorAceptar = false, ninos = perfiles } = {}) {
  if (sesion) localStorage.setItem('fishy.token', 'sesion-real')
  const enviar = vi.fn(async (url: string, opciones: RequestInit) => {
    let datos: unknown
    if (url.endsWith('/auth/perfil/')) datos = { ...padre, email }
    else if (url.endsWith('/invitaciones/consultar/')) datos = invitacion
    else if (url.endsWith('/jugadores/')) datos = ninos
    else if (url.endsWith('/invitaciones/aceptar/')) {
      if (errorAceptar) return new Response(JSON.stringify({ detail: 'Esta invitación ya no está disponible.' }), { status: 409 })
      datos = { jugador_id: JSON.parse(opciones.body as string).jugador_id ?? 30, grupo: invitacion.grupo, aceptada: true }
    } else if (url.endsWith('/auth/registro/')) datos = { token: 'registro', adulto_id: 4 }
    else if (url.endsWith('/auth/login/')) datos = { token: 'sesion-nueva', adulto_id: 4 }
    else throw new Error('Solicitud inesperada: ' + url)
    return new Response(JSON.stringify(datos), { status: url.endsWith('/auth/registro/') ? 201 : 200 })
  })
  vi.stubGlobal('fetch', enviar)
  const router = createMemoryRouter(rutas, { initialEntries: ['/invitacion#' + token] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return { user: userEvent.setup(), enviar, router }
}

describe('aceptación de una invitación', () => {
  it('solo ofrece el perfil invitado del padre autenticado y exige confirmación', async () => {
    const { user, enviar } = abrir()
    const selector = await screen.findByRole('combobox')
    expect(screen.queryByRole('option', { name: /Tomás/ })).toBeNull()
    expect(screen.queryByRole('option', { name: /6 años/ })).toBeNull()
    expect(screen.queryByRole('option', { name: /Crear/ })).toBeNull()
    const boton = screen.getByRole('button', { name: 'Aceptar y vincular a este niño' }) as HTMLButtonElement
    expect(boton.disabled).toBe(true)
    await user.selectOptions(selector, '10')
    expect(boton.disabled).toBe(true)
    await user.click(screen.getByRole('checkbox'))
    await user.click(boton)
    expect(await screen.findByText('Invitación aceptada')).toBeTruthy()
    const solicitudes = enviar.mock.calls.filter(c => c[0].endsWith('/invitaciones/aceptar/'))
    expect(solicitudes).toHaveLength(1)
    expect(JSON.parse(solicitudes[0][1].body as string)).toEqual({ token, confirmar: true, jugador_id: 10 })
    expect(screen.getByRole('link', { name: 'Ver su reporte' }).getAttribute('href')).toBe('/reportes/10')
  })
  it('otra cuenta no descarga perfiles ni puede aceptar', async () => {
    const { enviar } = abrir({ email: 'otra@example.com' })
    expect((await screen.findByRole('alert')).textContent).toContain('cuenta del padre o madre que recibió')
    expect(screen.queryByRole('combobox')).toBeNull()
    expect(enviar.mock.calls.some(c => c[0].endsWith('/jugadores/'))).toBe(false)
    expect(enviar.mock.calls.some(c => c[0].endsWith('/invitaciones/aceptar/'))).toBe(false)
  })
  it('crea únicamente el perfil indicado cuando la cuenta no tiene ese niño', async () => {
    const { user, enviar } = abrir({ ninos: [] })
    await user.selectOptions(await screen.findByRole('combobox'), 'nuevo')
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Aceptar y vincular a este niño' }))
    await screen.findByText('Invitación aceptada')
    const llamada = enviar.mock.calls.find(c => c[0].endsWith('/invitaciones/aceptar/'))!
    expect(JSON.parse(llamada[1].body as string)).toEqual({ token, confirmar: true, crear_perfil: true })
  })
  it('conserva la invitación al registrarse e iniciar sesión, sin aceptar automáticamente', async () => {
    const { user, enviar, router } = abrir({ sesion: false, ninos: [] })
    await user.click(await screen.findByRole('tab', { name: 'Registrarse' }))
    const email = screen.getByLabelText('Correo electrónico') as HTMLInputElement
    expect(email.value).toBe(invitacion.email)
    expect(email.readOnly).toBe(true)
    expect(screen.queryByRole('button', { name: 'Probar como padre' })).toBeNull()
    await user.type(screen.getByLabelText('Nombre de usuario'), 'padre_nuevo')
    await user.type(screen.getByLabelText('Contraseña', { exact: true }), 'Clave-larga-24')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave-larga-24')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta', exact: true }))
    await user.click(await screen.findByRole('button', { name: 'Ir a iniciar sesión' }))
    expect((screen.getByLabelText('Nombre de usuario') as HTMLInputElement).value).toBe('padre_nuevo')
    await user.type(screen.getByLabelText('Contraseña', { exact: true }), 'Clave-larga-24')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión', exact: true }))
    await screen.findByRole('combobox')
    expect(router.state.location.pathname).toBe('/invitacion')
    expect(router.state.location.hash).toBe('#' + token)
    expect(enviar.mock.calls.some(c => c[0].endsWith('/invitaciones/aceptar/'))).toBe(false)
  })
  it('si vence durante la aceptación informa el fallo sin simular el vínculo', async () => {
    const { user } = abrir({ errorAceptar: true })
    await user.selectOptions(await screen.findByRole('combobox'), '10')
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Aceptar y vincular a este niño' }))
    expect((await screen.findByRole('alert')).textContent).toContain('ya no está disponible')
    expect(screen.queryByText('Invitación aceptada')).toBeNull()
    await waitFor(() => expect((screen.getByRole('button', { name: 'Aceptar y vincular a este niño' }) as HTMLButtonElement).disabled).toBe(false))
  })
})
