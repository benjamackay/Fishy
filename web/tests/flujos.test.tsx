import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'
import * as pdf from '@/lib/pdf'
import * as demo from '@/mocks/gruposMock'

function abrir(ruta = '/login', cuenta?: 'principal' | 'alternativa') {
  if (cuenta) sessionStorage.setItem('fishy.demo.sesion.v2', cuenta)
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
    expect(guardar.mock.calls[0][1]).toBe(true)
    expect(await screen.findByText('PDF generado. La descarga está lista en tu navegador.')).toBeTruthy()
  })
  it('protege reportes antes de iniciar sesión y la demo no solicita endpoints', async () => {
    const fetch = vi.fn().mockRejectedValue(new Error('No debe usar red'))
    vi.stubGlobal('fetch', fetch)
    const { user } = abrir('/reportes/101')
    await user.click(await screen.findByRole('button', { name: 'Probar como padre' }))
    expect(await screen.findByRole('heading', { name: 'El progreso de Martina' })).toBeTruthy()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('al cambiar de tutor no muestra los niños de la cuenta anterior', async () => {
    const { user } = abrir('/', 'principal')
    expect(await screen.findByRole('link', { name: 'Ver reporte de Martina' })).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))

    await user.click(screen.getByRole('button', { name: 'Probar como profesor' }))
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
  it('crea, agrega por correo, informa duplicados y confirma antes de eliminar', async () => {
    const { user } = abrir('/admin/grupos', 'alternativa')
    await user.click(await screen.findByRole('button', { name: 'Crear grupo', exact: true }))
    let dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Nombre del grupo'), 'Tutorías')
    await user.click(within(dialogo).getByRole('button', { name: 'Crear grupo', exact: true }))
    expect(await screen.findByRole('heading', { name: 'Tutorías' })).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('Grupo creado correctamente')
    expect(screen.getByText(/Identificador único:/).textContent).toMatch(/[0-9a-f]{8}-/)
    await user.click(screen.getByRole('button', { name: 'Agregar usuario', exact: true }))
    dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Correo electrónico'), 'familia.silva@example.com')
    await user.click(within(dialogo).getByRole('button', { name: 'Agregar', exact: true }))
    expect(await screen.findByText('familia.silva@example.com')).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('Usuario agregado')
    await user.click(screen.getByRole('button', { name: 'Agregar usuario', exact: true }))
    dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Correo electrónico'), 'FAMILIA.SILVA@example.com')
    await user.click(within(dialogo).getByRole('button', { name: 'Agregar', exact: true }))
    expect((await within(dialogo).findByRole('alert')).textContent).toContain('ya forma parte')
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    await user.click(screen.getByRole('button', { name: 'Eliminar usuario familia.silva@example.com' }))
    dialogo = screen.getByRole('dialog')
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    expect(screen.getByText('familia.silva@example.com')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Eliminar usuario familia.silva@example.com' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirmar eliminación' }))
    await waitFor(() => expect(screen.queryByText('familia.silva@example.com')).toBeNull())
    expect(screen.getByRole('status').textContent).toContain('Usuario eliminado')
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
    await user.click(screen.getByText('Probar la actualización automática'))
    await user.click(screen.getByRole('button', { name: 'Simular nivel completado' }))
    await waitFor(() => expect(within(retos).getByRole('meter').getAttribute('aria-valuenow')).toBe('80'))
    await user.click(within(screen.getByRole('navigation', { name: 'Navegación principal' })).getByRole('link', { name: 'Reportes', exact: true }))
    await user.click(await screen.findByRole('link', { name: 'Ver reporte de Martina' }))
    await waitFor(() => expect(screen.getByRole('meter', { name: 'Decisiones seguras: Retos Virales' }).getAttribute('aria-valuenow')).toBe('80'))
  })
})
