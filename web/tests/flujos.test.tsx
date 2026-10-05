import { describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'
import { prepararCuenta } from './fixtures/sesion'
import * as pdf from '@/lib/pdf'
import * as demo from './fixtures/gruposMock'

function abrir(ruta = '/login', cuenta?: 'principal' | 'alternativa') {
  if (cuenta) prepararCuenta(cuenta)
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] })
  const vista = render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return { ...vista, router, user: userEvent.setup() }
}
describe('criterios de aceptación en las pantallas', () => {
  it('un fallo de login real no crea una sesión ni activa datos ficticios', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 400 })))
    const { user } = abrir()
    await user.type(screen.getByLabelText('Nombre de usuario'), 'cuenta-invalida')
    await user.type(screen.getByLabelText('Contraseña'), 'incorrecta')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión', exact: true }))
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(sessionStorage.getItem('fishy.demo.sesion.v2')).toBeNull()
    expect(screen.queryByRole('link', { name: 'Ver reporte de Martina' })).toBeNull()
  })
  it('descargar genera un PDF con el último agregado disponible', async () => {
    const guardar = vi.spyOn(pdf, 'descargarPdfGrupo').mockImplementation(() => {})
    const fuente = demo.crearPanelDemo(1002)
    const anterior = await fuente.obtenerReporteGrupo('grupo-demo-1002-a')
    const leer = vi.spyOn(fuente, 'obtenerReporteGrupo').mockResolvedValue(anterior)
    vi.spyOn(demo, 'crearPanelDemo').mockReturnValue(fuente)
    const { user } = abrir('/admin/grupos/grupo-demo-1002-a/reporte', 'alternativa')
    await screen.findByRole('heading', { name: '5° Básico A' })
    // El servicio entrega un agregado nuevo, sin que el profesor acceda a un niño.
    leer.mockResolvedValue({ ...anterior, tematicas: anterior.tematicas.map(t => t.tematica === 'desconocidos'
      ? { ...t, metricas: { decisiones_seguras: 36, decisiones_evaluadas: 45, completada: true } }
      : t) })
    await user.click(screen.getByRole('button', { name: 'Descargar reporte' }))
    await waitFor(() => expect(guardar).toHaveBeenCalledTimes(1))
    expect(guardar.mock.calls[0][0].tematicas[0].metricas?.decisiones_evaluadas).toBe(45)
    expect(guardar.mock.calls[0]).toHaveLength(1)
    expect(await screen.findByText('PDF generado. Revisa las descargas de tu navegador.')).toBeTruthy()
  })
  it('protege reportes antes de iniciar sesión y vuelve al reporte después del login', async () => {
    const fetch = vi.fn().mockRejectedValue(new Error('No debe usar red'))
    vi.stubGlobal('fetch', fetch)
    const { user } = abrir('/reportes/101')
    prepararCuenta('principal', false)
    await user.type(await screen.findByLabelText('Nombre de usuario'), 'camila')
    await user.type(screen.getByLabelText('Contraseña'), 'clave-de-prueba')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión', exact: true }))
    expect(await screen.findByRole('heading', { name: 'El progreso de Martina' })).toBeTruthy()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('al cambiar de tutor no muestra los niños de la cuenta anterior', async () => {
    const { user } = abrir('/', 'principal')
    expect(await screen.findByRole('link', { name: 'Ver reporte de Martina' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))

    prepararCuenta('alternativa', false)
    await user.type(await screen.findByLabelText('Nombre de usuario'), 'diego')
    await user.type(screen.getByLabelText('Contraseña'), 'clave-de-prueba')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión', exact: true }))
    expect(await screen.findByRole('heading', { name: 'Mis grupos' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Reportes', exact: true })).toBeNull()
    expect(screen.queryByText('Sofía')).toBeNull()
    expect(screen.queryByText('Martina')).toBeNull()
    expect(screen.queryByText('Tomás')).toBeNull()
  })
  it('deniega un reporte ajeno incluso al escribir directamente su URL', async () => {
    abrir('/reportes/103', 'principal')
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.getByRole('alert').textContent).toContain('No tienes acceso')
    expect(screen.queryByText('Sofía')).toBeNull()
    expect(screen.queryAllByRole('meter')).toHaveLength(0)
  })
  it('crea un grupo y agrega solo los hermanos seleccionados por correo', async () => {
    const { user } = abrir('/admin/grupos', 'alternativa')
    await user.click(await screen.findByRole('button', { name: 'Crear grupo', exact: true }))
    let dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Nombre del grupo'), 'Tutorías')
    await user.click(within(dialogo).getByRole('button', { name: 'Crear grupo', exact: true }))
    expect(await screen.findByRole('heading', { name: 'Tutorías' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Invitar familia' })).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Agregar', exact: true }))
    dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Correo del apoderado'), 'FAMILIA.SILVA@example.com')
    await user.click(within(dialogo).getByRole('button', { name: 'Buscar' }))
    const sofia = await within(dialogo).findByRole('checkbox', { name: /Sofía/ }) as HTMLInputElement
    expect(sofia.disabled).toBe(true)
    expect(sofia.checked).toBe(false)
    await user.click(within(dialogo).getByRole('checkbox', { name: /Valentina/ }))
    await user.click(within(dialogo).getByRole('button', { name: 'Agregar', exact: true }))
    const confirmacion = await screen.findByRole('dialog', { name: 'Niños agregados al curso' })
    expect(confirmacion.textContent).toContain('Los niños seleccionados ya forman parte del curso.')
    await user.click(within(confirmacion).getByRole('button', { name: 'Entendido' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(await screen.findByRole('button', { name: 'Eliminar integrante Valentina' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Eliminar integrante Mateo' })).toBeNull()
    expect(screen.getByText('1 perfil vinculado')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Agregar', exact: true }))
    dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Correo del apoderado'), 'familia.silva@example.com')
    await user.click(within(dialogo).getByRole('button', { name: 'Buscar' }))
    const valentina = await within(dialogo).findByRole('checkbox', { name: /Valentina/ }) as HTMLInputElement
    expect(valentina.disabled).toBe(true)
    expect(valentina.checked).toBe(true)
  })
  it('quitar un integrante exige confirmar y conserva el resto del grupo', async () => {
    const { user } = abrir('/admin/grupos/grupo-demo-1002-a', 'alternativa')
    await user.click(await screen.findByRole('button', { name: 'Eliminar integrante Sofía' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Volver' }))
    expect(screen.getByText('Sofía')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Eliminar integrante Sofía' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirmar' }))
    await waitFor(() => expect(screen.queryByText('Sofía')).toBeNull())
    expect(screen.getByText('Lucas')).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('Su perfil y progreso se conservan')
  })
  it('oculta las invitaciones existentes sin borrar su información', async () => {
    const fuente = demo.crearPanelDemo(1002)
    const id = 'grupo-demo-1002-b'
    await fuente.invitarFamilia(id, { email: 'pendiente@example.com', nombre_nino: 'Invitado pendiente' })
    abrir('/admin/grupos/' + id, 'alternativa')
    await screen.findByRole('heading', { name: 'Taller de bienvenida' })
    expect(screen.queryByRole('button', { name: 'Invitar familia' })).toBeNull()
    expect(screen.queryByRole('region', { name: 'Invitaciones del grupo' })).toBeNull()
    expect(screen.queryByText('Invitado pendiente')).toBeNull()
    expect((await fuente.obtenerGrupo(id)).invitaciones).toHaveLength(1)
  })
  it('muestra el reporte grupal sin correos ni resultados individuales', async () => {
    const { container } = abrir('/admin/grupos/grupo-demo-1002-a/reporte', 'alternativa')
    expect(await screen.findByRole('heading', { name: '5° Básico A' })).toBeTruthy()
    expect(screen.getAllByRole('meter')).toHaveLength(3)
    expect(container.textContent).not.toMatch(/@|Sofía|Lucas|Emilia|Agustín/)
    expect((screen.getByRole('button', { name: 'Descargar reporte' }) as HTMLButtonElement).disabled).toBe(false)
  })
  it('un grupo vacío informa ausencia de datos y no permite descargar', async () => {
    const { container } = abrir('/admin/grupos/grupo-demo-1002-b/reporte', 'alternativa')
    expect(await screen.findByRole('heading', { name: 'Aún no hay datos disponibles para el análisis grupal' })).toBeTruthy()
    expect(screen.queryAllByRole('meter')).toHaveLength(0)
    expect(container.textContent).not.toContain('%')
    expect((screen.getByRole('button', { name: 'Descargar reporte' }) as HTMLButtonElement).disabled).toBe(true)
  })
  it('muestra un nivel nuevo automáticamente y conserva el resultado al volver al reporte', async () => {
    const { user } = abrir('/reportes/101', 'principal')
    await screen.findByRole('heading', { name: 'El progreso de Martina' })
    const retos = screen.getByRole('region', { name: 'Retos Virales' })
    expect(within(retos).queryByRole('meter')).toBeNull()
    await act(async () => { await demo.simularProgreso(1001, 101, 'retos_virales') })
    await waitFor(() => expect(within(retos).getByRole('meter').getAttribute('aria-valuenow')).toBe('80'))
    await user.click(within(screen.getByRole('navigation', { name: 'Navegación principal' })).getByRole('link', { name: 'Reportes', exact: true }))
    await user.click(await screen.findByRole('link', { name: 'Ver reporte de Martina' }))
    await waitFor(() => expect(screen.getByRole('meter', { name: 'Decisiones seguras: Retos Virales' }).getAttribute('aria-valuenow')).toBe('80'))
  })
})
