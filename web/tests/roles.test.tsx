import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'
import { aplicarPermisosPanel } from '@/lib/permisosPanel'
import { crearPanelDemo } from '@/mocks/gruposMock'
import * as demo from '@/mocks/gruposMock'
import { perfilesDemo } from '@/mocks/sesionDemo'
import * as auth from '@/api/auth'
import { panelReal } from '@/api/panelReal'
import type { FuentePanel } from '@/types/panel'
import type { AdultoResponsable } from '@/types/api'

function abrir(ruta: string, cuenta?: 'principal' | 'alternativa') {
  if (cuenta) sessionStorage.setItem('fishy.demo.sesion.v2', cuenta)
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] })
  render(<ProveedorSesion><RouterProvider router={router} /></ProveedorSesion>)
  return userEvent.setup()
}
describe('tutores padres y administradores', () => {
  it('el padre ve sus hijos y no tiene navegación ni botones de grupos', async () => {
    abrir('/', 'principal')
    await screen.findByRole('link', { name: 'Ver reporte de Martina' })
    expect(screen.getByRole('link', { name: 'Ver reporte de Tomás' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Mis grupos' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Crear grupo' })).toBeNull()
    expect(screen.queryByText('Sofía')).toBeNull()
    expect(screen.getByText('Tutor padre/madre')).toBeTruthy()
  })
  it.each([
    '/admin',
    '/admin/grupos',
    '/admin/grupos/grupo-demo-1001-a',
    '/admin/grupos/grupo-demo-1001-a/reporte',
  ])('el padre no puede abrir directamente %s ni consultar sus datos', async ruta => {
    abrir(ruta, 'principal')
    await screen.findByRole('heading', { name: 'Acceso exclusivo para profesores' })
    expect(screen.queryByRole('link', { name: 'Mis grupos' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Crear grupo' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Descargar reporte' })).toBeNull()
    expect(screen.queryByText('5° Básico A')).toBeNull()
    // La pantalla bloqueada no llega a cargar/sembrar datos de grupos.
    expect(localStorage.getItem('fishy.demo.panel.v2.1001')).toBeNull()
  })
  it('la demo de profesor inicia en grupos y permite crear', async () => {
    const user = abrir('/login')
    await user.click(screen.getByRole('button', { name: 'Probar como profesor' }))
    await screen.findByRole('heading', { name: 'Mis grupos' })
    expect(screen.getByText('Tutor administrador')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Crear grupo', exact: true }))
    const dialogo = screen.getByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Nombre del grupo'), 'Curso del profesor')
    await user.click(within(dialogo).getByRole('button', { name: 'Crear grupo', exact: true }))
    expect(await screen.findByRole('heading', { name: 'Curso del profesor' })).toBeTruthy()
  })
  it('cambiar de profesor a padre retira el grupo que estaba abierto', async () => {
    const user = abrir('/admin/grupos/grupo-demo-1002-a/reporte', 'alternativa')
    await screen.findByRole('heading', { name: '5° Básico A' })
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
    await user.click(await screen.findByRole('button', { name: 'Probar como padre' }))
    await screen.findByRole('heading', { name: 'Acceso exclusivo para profesores' })
    expect(screen.queryByRole('button', { name: 'Descargar reporte' })).toBeNull()
    expect(screen.queryByText('5° Básico A')).toBeNull()
    await user.click(screen.getByRole('link', { name: 'Ver los reportes de mis hijos' }))
    expect(await screen.findByRole('link', { name: 'Ver reporte de Martina' })).toBeTruthy()
  })
  it.each([false, undefined])('el perfil real con is_admin=%s no recibe administración', async rol => {
    localStorage.setItem('fishy.token', 'token-prueba')
    vi.stubEnv('VITE_FORZAR_ADMIN', 'true')
    vi.spyOn(auth, 'obtenerPerfil').mockResolvedValue({ ...perfilesDemo.principal, is_admin: rol })
    abrir('/admin/grupos')
    await screen.findByRole('heading', { name: 'Acceso exclusivo para profesores' })
    expect(screen.queryByRole('link', { name: 'Mis grupos' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Crear grupo' })).toBeNull()
    vi.unstubAllEnvs()
  })
  it('restaura una sesión real de profesor únicamente en grupos', async () => {
    localStorage.setItem('fishy.token', 'token-prueba')
    vi.spyOn(auth, 'obtenerPerfil').mockResolvedValue(perfilesDemo.alternativa)
    const grupos = vi.spyOn(panelReal, 'listarGrupos').mockResolvedValue([])
    const ninos = vi.spyOn(panelReal, 'listarNinos')
    const reporte = vi.spyOn(panelReal, 'obtenerReporteNino')
    abrir('/reportes/103')
    await screen.findByRole('heading', { name: 'Mis grupos' })
    expect(grupos).toHaveBeenCalled()
    expect(ninos).not.toHaveBeenCalled()
    expect(reporte).not.toHaveBeenCalled()
    expect(screen.queryByRole('link', { name: 'Reportes', exact: true })).toBeNull()
  })
  it.each(['/', '/reportes', '/reportes/103', '/partidas/103'])('el profesor que abre %s vuelve a grupos sin cargar niños', async ruta => {
    const fuente = crearPanelDemo(1002)
    const listar = vi.spyOn(fuente, 'listarNinos')
    const consultar = vi.spyOn(fuente, 'obtenerReporteNino')
    vi.spyOn(demo, 'crearPanelDemo').mockReturnValue(fuente)
    const user = abrir(ruta, 'alternativa')
    await screen.findByRole('heading', { name: 'Mis grupos' })
    const navegacion = screen.getByRole('navigation', { name: 'Navegación principal' })
    expect(within(navegacion).getAllByRole('link')).toHaveLength(1)
    expect(within(navegacion).getByRole('link', { name: 'Mis grupos' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Reportes', exact: true })).toBeNull()
    expect(screen.queryByText('Sofía')).toBeNull()
    await user.click(screen.getByRole('link', { name: 'Fishy! Inicio' }))
    expect(screen.getByRole('heading', { name: 'Mis grupos' })).toBeTruthy()
    expect(listar).not.toHaveBeenCalled()
    expect(consultar).not.toHaveBeenCalled()
  })
  it('corrige los vínculos antiguos del profesor sin perder sus grupos ni resultados', async () => {
    const panel = crearPanelDemo(1002)
    const antes = await panel.obtenerReporteGrupo('grupo-demo-1002-a')
    const grupo = await panel.crearGrupo({ nombre: 'Grupo que debe conservarse' })
    const clave = 'fishy.demo.panel.v2.1002'
    const antiguo = JSON.parse(localStorage.getItem(clave)!)
    antiguo.ninos[0].nino.adulto_id = 1002
    localStorage.setItem(clave, JSON.stringify(antiguo))

    expect(await panel.listarNinos()).toEqual([])
    await expect(panel.obtenerReporteNino(103)).rejects.toThrow('No tienes acceso')
    expect((await panel.obtenerGrupo(grupo.id)).nombre).toBe('Grupo que debe conservarse')
    expect(await panel.obtenerReporteGrupo('grupo-demo-1002-a')).toEqual(antes)
    const guardado = JSON.parse(localStorage.getItem(clave)!)
    expect(guardado.ninos.some((r: { nino: { adulto_id: number } }) => r.nino.adulto_id === 1002)).toBe(false)
  })
})

describe('permisos antes de invocar el servicio', () => {
  const operaciones: Array<(panel: FuentePanel) => Promise<unknown>> = [
    p => p.listarGrupos(),
    p => p.crearGrupo({ nombre: 'No permitido' }),
    p => p.obtenerGrupo('grupo'),
    p => p.invitarFamilia('grupo', { email: 'familia@example.com', nombre_nino: 'Martina' }),
    p => p.reenviarInvitacion('grupo', 'invitacion'),
    p => p.cancelarInvitacion('grupo', 'invitacion'),
    p => p.eliminarUsuario('grupo', 'miembro'),
    p => p.eliminarGrupo('grupo'),
    p => p.obtenerReporteGrupo('grupo'),
    p => p.obtenerSeguimientoGrupo('grupo'),
  ]
  it.each([false, undefined, 'true', 1, null])('deniega todas las operaciones grupales con un rol no autorizado: %s', async rol => {
    const fuente = crearPanelDemo(1001)
    const espias = ['listarGrupos', 'crearGrupo', 'obtenerGrupo', 'invitarFamilia', 'reenviarInvitacion', 'cancelarInvitacion', 'eliminarUsuario', 'eliminarGrupo', 'obtenerReporteGrupo', 'obtenerSeguimientoGrupo'].map(nombre => vi.spyOn(fuente, nombre as keyof FuentePanel))
    const perfil = { ...perfilesDemo.principal, is_admin: rol } as AdultoResponsable
    const panel = aplicarPermisosPanel(fuente, perfil)
    for (const operacion of operaciones) await expect(operacion(panel)).rejects.toThrow('Solo los tutores administradores')
    for (const espia of espias) expect(espia).not.toHaveBeenCalled()
  })
  it('autoriza el flujo grupal completo para un profesor explícito', async () => {
    const panel = aplicarPermisosPanel(crearPanelDemo(1002), perfilesDemo.alternativa)
    expect((await panel.listarGrupos()).length).toBeGreaterThan(0)
    const g = await panel.crearGrupo({ nombre: 'Curso' })
    expect((await panel.obtenerGrupo(g.id)).nombre).toBe('Curso')
    const invitacion = await panel.invitarFamilia(g.id, { email: 'familia.silva@example.com', nombre_nino: 'Sofía' })
    await panel.reenviarInvitacion(g.id, invitacion.id)
    await panel.cancelarInvitacion(g.id, invitacion.id)
    expect((await panel.obtenerReporteGrupo(g.id)).total_integrantes).toBe(0)
    await panel.eliminarGrupo(g.id)
    await expect(panel.obtenerGrupo(g.id)).rejects.toThrow('No encontramos')
  })
  it('rechaza lecturas individuales del profesor antes de consultar el servicio, incluso con un vínculo antiguo', async () => {
    const fuente = crearPanelDemo(1002)
    const listar = vi.spyOn(fuente, 'listarNinos').mockResolvedValue([
      { id: 103, adulto_id: 1002, nombre: 'Perfil antiguo', edad: 10, actualizado_en: null },
    ])
    const consultar = vi.spyOn(fuente, 'obtenerReporteNino')
    const panel = aplicarPermisosPanel(fuente, perfilesDemo.alternativa)
    await expect(panel.listarNinos()).rejects.toThrow('solo tienen acceso a grupos')
    await expect(panel.obtenerReporteNino(103)).rejects.toThrow('solo tienen acceso a grupos')
    expect(listar).not.toHaveBeenCalled()
    expect(consultar).not.toHaveBeenCalled()
  })
  it('una sesión anónima no consulta perfiles ni grupos', async () => {
    const fuente = crearPanelDemo(1001)
    const listar = vi.spyOn(fuente, 'listarNinos')
    const panel = aplicarPermisosPanel(fuente, null)
    await expect(panel.listarNinos()).rejects.toThrow('Inicia sesión')
    await expect(panel.obtenerReporteNino(101)).rejects.toThrow('Inicia sesión')
    await expect(panel.listarGrupos()).rejects.toThrow('Solo los tutores administradores')
    expect(listar).not.toHaveBeenCalled()
  })
})
