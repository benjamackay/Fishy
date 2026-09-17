import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'
import * as demo from '@/mocks/gruposMock'
import { correoSeguimiento, evaluarSeguimientoDemo } from '@/lib/seguimiento'
import { ApiError } from '@/lib/api'
import { EVENTO_DATOS } from '@/hooks/useDatosVivos'

const grupo = 'grupo-demo-1002-a'
const fuente = () => demo.crearPanelDemo(1002)
function abrir(ruta = '/admin/grupos/' + grupo) {
  sessionStorage.setItem('fishy.demo.sesion.v2', 'alternativa')
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return userEvent.setup()
}
const region = () => screen.getByRole('region', { name: 'Alumnos que necesitan apoyo' })

describe('seguimiento del curso', () => {
  it('muestra motivos por tema, cero real, prioridad y un borrador sin enviar correo', async () => {
    const fetch = vi.fn().mockRejectedValue(new Error('La demo no usa red'))
    vi.stubGlobal('fetch', fetch)
    await demo.simularSeguimiento(1002, grupo)
    abrir()
    const sofia = await screen.findByRole('article', { name: 'Seguimiento de Sofía' })
    expect(sofia.textContent).toContain('Atención prioritaria')
    expect(sofia.textContent).toContain('0 de 10 decisiones evaluadas')
    expect(sofia.textContent).toContain('0%')
    expect(sofia.textContent).toContain('reconocer contactos desconocidos')
    const lucas = screen.getByRole('article', { name: 'Seguimiento de Lucas' })
    expect(lucas.textContent).toContain('Necesita apoyo')
    expect(lucas.textContent).toContain('4 de 10 decisiones evaluadas')
    expect(within(region()).getByRole('button', { name: 'Necesitan apoyo 2' }).getAttribute('aria-pressed')).toBe('true')
    const enlace = within(sofia).getByRole('link', { name: 'Preparar correo a la familia' }).getAttribute('href')!
    const url = new URL(enlace)
    expect(decodeURIComponent(url.pathname)).toBe('familia.silva@example.com')
    expect(url.searchParams.get('body')).toContain('Sofía')
    expect(url.searchParams.get('body')).toContain('Desconocidos, Ciberacoso')
    expect(url.searchParams.get('body')).not.toMatch(/prioritaria|deficiente|%/)
    expect(fetch).not.toHaveBeenCalled()
  })

  it('separa muestras insuficientes y ausencia de datos sin porcentajes infantiles', async () => {
    await demo.simularSeguimiento(1002, grupo)
    const user = abrir()
    await screen.findByRole('article', { name: 'Seguimiento de Sofía' })
    await user.click(within(region()).getByRole('button', { name: 'Sin evaluación suficiente 2' }))
    const emilia = screen.getByRole('article', { name: 'Seguimiento de Emilia' })
    expect(emilia.textContent).toContain('3 de 5 decisiones mínimas')
    expect(emilia.textContent).not.toContain('%')
    const agustin = screen.getByRole('article', { name: 'Seguimiento de Agustín' })
    expect(agustin.textContent).toContain('Sin datos disponibles')
    expect(agustin.textContent).not.toContain('%')
    expect(within(agustin).queryByRole('link')).toBeNull()
  })

  it('los controles demo actualizan alertas y una mejora las retira sin recargar', async () => {
    const user = abrir()
    await screen.findByRole('region', { name: 'Alumnos que necesitan apoyo' })
    await within(region()).findByRole('heading', { name: 'Sin alertas de apoyo en las temáticas evaluadas' })
    await user.click(screen.getByText('Probar casos de apoyo'))
    await user.click(screen.getByRole('button', { name: 'Simular casos de apoyo' }))
    await screen.findByRole('article', { name: 'Seguimiento de Sofía' })
    await user.click(screen.getByRole('button', { name: 'Simular mejora' }))
    await waitFor(() => expect(screen.queryByRole('article', { name: 'Seguimiento de Sofía' })).toBeNull())
    expect(within(region()).getByRole('button', { name: 'Necesitan apoyo 0' })).toBeTruthy()
    await user.click(within(region()).getByRole('button', { name: 'Todos 4' }))
    expect(screen.getAllByRole('article')).toHaveLength(4)
    expect(screen.getByRole('article', { name: 'Seguimiento de Sofía' }).textContent).toContain('90%')
  })

  it('al quitar el vínculo desaparece también el seguimiento y al cambiar de grupo no se conserva', async () => {
    await demo.simularSeguimiento(1002, grupo)
    const user = abrir()
    await screen.findByRole('article', { name: 'Seguimiento de Sofía' })
    await user.click(screen.getByRole('button', { name: 'Eliminar integrante Sofía' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirmar' }))
    await waitFor(() => expect(screen.queryByRole('article', { name: 'Seguimiento de Sofía' })).toBeNull())
    expect(within(region()).getByRole('button', { name: 'Necesitan apoyo 1' })).toBeTruthy()
    await user.click(within(screen.getByRole('navigation', { name: 'Navegación principal' })).getByRole('link', { name: 'Mis grupos' }))
    await screen.findByRole('heading', { name: 'Mis grupos' })
    expect(screen.queryByRole('article', { name: 'Seguimiento de Lucas' })).toBeNull()
  })

  it('un fallo de red identifica el último dato conocido y un permiso revocado lo retira', async () => {
    await demo.simularSeguimiento(1002, grupo)
    const panel = fuente()
    const consultar = vi.spyOn(panel, 'obtenerSeguimientoGrupo')
    vi.spyOn(demo, 'crearPanelDemo').mockReturnValue(panel)
    abrir()
    await screen.findByRole('article', { name: 'Seguimiento de Sofía' })
    consultar.mockRejectedValue(new Error('Sin conexión'))
    act(() => window.dispatchEvent(new Event(EVENTO_DATOS)))
    await within(region()).findByText('Se muestra la última consulta disponible. No pudimos comprobar si hay cambios.')
    expect(screen.getByRole('article', { name: 'Seguimiento de Sofía' })).toBeTruthy()
    consultar.mockRejectedValue(new ApiError(403, { detail: 'Sin acceso' }))
    act(() => window.dispatchEvent(new Event('focus')))
    await waitFor(() => expect(within(region()).queryAllByRole('article')).toHaveLength(0))
    expect(within(region()).queryByRole('button', { name: 'Necesitan apoyo 0' })).toBeNull()
  })

  it('el seguimiento consulta al volver al panel y no abre el reporte individual del profesor', async () => {
    const panel = fuente()
    const consultar = vi.spyOn(panel, 'obtenerSeguimientoGrupo')
    const individual = vi.spyOn(panel, 'obtenerReporteNino')
    vi.spyOn(demo, 'crearPanelDemo').mockReturnValue(panel)
    abrir()
    await screen.findByRole('region', { name: 'Alumnos que necesitan apoyo' })
    await within(region()).findByRole('button', { name: 'Todos 4' })
    const antes = consultar.mock.calls.length
    act(() => window.dispatchEvent(new Event('focus')))
    await waitFor(() => expect(consultar.mock.calls.length).toBeGreaterThan(antes))
    expect(individual).not.toHaveBeenCalled()
  })

  it('el grupo vacío explica que necesita vínculos y no inventa alumnos', async () => {
    abrir('/admin/grupos/grupo-demo-1002-b')
    await screen.findByRole('heading', { name: 'Aún no hay alumnos vinculados' })
    expect(within(region()).queryAllByRole('article')).toHaveLength(0)
    expect((screen.getByRole('button', { name: 'Simular casos de apoyo', hidden: true }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('el reporte grupal mantiene anonimato cuando existen alertas y enlaza al seguimiento', async () => {
    await demo.simularSeguimiento(1002, grupo)
    const user = abrir('/admin/grupos/' + grupo + '/reporte')
    const link = await screen.findByRole('link', { name: /Ver alumnos que necesitan apoyo/ })
    expect(document.body.textContent).not.toMatch(/Sofía|Lucas|familia.silva|prioritario/)
    await user.click(link)
    expect(await screen.findByRole('article', { name: 'Seguimiento de Sofía' })).toBeTruthy()
  })
})

describe('casos límite de la evaluación demo', () => {
  it.each([[0, 4, 'muestra_insuficiente', null], [0, 5, 'prioritario', 0], [2, 5, 'apoyo', 40], [3, 5, 'sin_alertas', 60], [6, 5, 'sin_datos', null], [-1, 5, 'sin_datos', null], [1, 0, 'sin_datos', null]] as const)('evalúa %i de %i sin confundir límites', async (seguras, evaluadas, estado, valor) => {
    const miembro = (await fuente().obtenerGrupo(grupo)).miembros[0]
    const r = await demo.crearPanelDemo(1001).obtenerReporteNino(101)
    r.tematicas = [{ tematica: 'desconocidos', metricas: { decisiones_seguras: seguras, decisiones_evaluadas: evaluadas, completada: true } }]
    const a = evaluarSeguimientoDemo(miembro, r, Date.now())
    expect(a.estado).toBe(estado)
    expect(a.tematicas[0].porcentaje_seguro).toBe(valor)
    expect(a.general).toBeNull()
  })

  it('señala resultados antiguos y rechaza fechas futuras o inválidas', async () => {
    const miembro = (await fuente().obtenerGrupo(grupo)).miembros[0]
    const r = await demo.crearPanelDemo(1001).obtenerReporteNino(101)
    const ahora = Date.now()
    r.actualizado_en = new Date(ahora - 31 * 86400000).toISOString()
    expect(evaluarSeguimientoDemo(miembro, r, ahora).tematicas[0].datos_antiguos).toBe(true)
    for (const fecha of ['invalida', new Date(ahora + 86400000).toISOString()]) {
      r.actualizado_en = fecha
      expect(evaluarSeguimientoDemo(miembro, r, ahora).estado).toBe('sin_datos')
    }
  })

  it('no permite inyectar destinatarios adicionales en el borrador', async () => {
    const a = (await fuente().obtenerSeguimientoGrupo(grupo)).alumnos[0]
    expect(correoSeguimiento({ ...a, email_familia: 'familia@example.com\r\nBcc:otro@example.com' }, 'Curso')).toBeNull()
    const url = new URL(correoSeguimiento({ ...a, email_familia: 'familia+prueba@example.com' }, 'Curso & Bcc: nadie')!)
    expect(url.searchParams.has('bcc')).toBe(false)
    expect(decodeURIComponent(url.pathname)).toBe('familia+prueba@example.com')
  })
})
