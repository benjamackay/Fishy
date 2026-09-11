import { describe, expect, it, vi } from 'vitest'
import { crearPanelDemo } from '@/mocks/gruposMock'
describe('gestión de grupos', () => {
  it('crea grupos con UUID distintos y persiste los cambios', async () => {
    const panel = crearPanelDemo(1001)
    const a = await panel.crearGrupo({ nombre: '  Taller A  ', descripcion: '  Curso  ' })
    const b = await panel.crearGrupo({ nombre: 'Taller A' })
    expect(a.id).toMatch(/^[0-9a-f]{8}-[0-9a-f-]{27}$/)
    expect(a.id).not.toBe(b.id)
    expect(a.nombre).toBe('Taller A')
    expect((await crearPanelDemo(1001).obtenerGrupo(a.id)).descripcion).toBe('Curso')
    await expect(panel.crearGrupo({ nombre: '   ' })).rejects.toThrow('nombre')
    await expect(panel.crearGrupo({ nombre: 'x'.repeat(81) })).rejects.toThrow('nombre')
  })
  it('invita por niño y correo normalizado e impide duplicados concurrentes', async () => {
    const panel = crearPanelDemo(1001)
    const g = await panel.crearGrupo({ nombre: 'Correos' })
    const resultados = await Promise.allSettled([
      panel.invitarFamilia(g.id, { email: '  FAMILIA.ROJAS@example.com  ', nombre_nino: 'Martina' }),
      panel.invitarFamilia(g.id, { email: 'familia.rojas@example.com', nombre_nino: ' martina ' }),
    ])
    expect(resultados.filter(r => r.status === 'fulfilled')).toHaveLength(1)
    expect(resultados.filter(r => r.status === 'rejected')).toHaveLength(1)
    expect((await panel.obtenerGrupo(g.id)).miembros).toHaveLength(0)
    expect((await panel.obtenerGrupo(g.id)).invitaciones).toHaveLength(1)
    await expect(panel.invitarFamilia(g.id, { email: 'familia.rojas@example.com', nombre_nino: 'Martina' })).rejects.toThrow('Ya existe una invitación')
    await panel.invitarFamilia(g.id, { email: 'familia.rojas@example.com', nombre_nino: 'Tomás' })
    expect((await panel.obtenerGrupo(g.id)).invitaciones).toHaveLength(2)
  })
  it('admite familias nuevas, no finge correo real y permite reenviar y cancelar', async () => {
    const panel = crearPanelDemo(1001)
    const g = await panel.crearGrupo({ nombre: 'Grupo' })
    await expect(panel.invitarFamilia(g.id, { email: 'invalido', nombre_nino: 'Martina' })).rejects.toThrow('válido')
    const inv = await panel.invitarFamilia(g.id, { email: 'nueva@example.com', nombre_nino: 'Martina' })
    expect(inv.estado_envio).toBe('simulado')
    expect((await panel.reenviarInvitacion(g.id, inv.id)).estado_envio).toBe('simulado')
    await panel.cancelarInvitacion(g.id, inv.id)
    await expect(panel.reenviarInvitacion(g.id, inv.id)).rejects.toThrow('ya no está disponible')
    expect((await panel.obtenerGrupo(g.id)).miembros).toHaveLength(0)
    await panel.invitarFamilia(g.id, { email: 'nueva@example.com', nombre_nino: 'Martina' })
    expect((await panel.obtenerGrupo(g.id)).invitaciones.filter(i => i.estado === 'pendiente')).toHaveLength(1)
  })
  it('eliminar un grupo no elimina usuarios ni reportes', async () => {
    const panel = crearPanelDemo(1001)
    await panel.eliminarGrupo('grupo-demo-1001-a')
    expect((await panel.listarGrupos()).some(g => g.id === 'grupo-demo-1001-a')).toBe(false)
    expect((await panel.obtenerReporteNino(101)).nino.nombre).toBe('Martina')
  })
  it('permite crear y consultar en memoria si el navegador bloquea almacenamiento', async () => {
    const get = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('bloqueado') })
    const set = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('bloqueado') })
    const panel = crearPanelDemo(2001)
    const grupo = await panel.crearGrupo({ nombre: 'En memoria' })
    expect((await panel.obtenerGrupo(grupo.id)).nombre).toBe('En memoria')
    get.mockRestore(); set.mockRestore()
  })
})
