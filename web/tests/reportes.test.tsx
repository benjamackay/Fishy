import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import { Tematicas } from '@/components/Reportes'
import { crearPanelDemo, simularProgreso } from '@/mocks/gruposMock'
import { porcentajeSeguro } from '@/lib/reportes'
import { crearPdfGrupo } from '@/lib/pdf'
import type { ReporteGrupo } from '@/types/reportes'

describe('reportes y privacidad', () => {
  it('solo lista niños de la cuenta y rechaza el acceso directo a otro niño', async () => {
    const camila = crearPanelDemo(1001)
    const diego = crearPanelDemo(1002)
    expect((await camila.listarNinos()).map(n => n.nombre)).toEqual(['Martina', 'Tomás'])
    expect(await diego.listarNinos()).toEqual([])
    await expect(diego.obtenerReporteNino(103)).rejects.toThrow('No tienes acceso')
    await expect(camila.obtenerReporteNino(103)).rejects.toThrow('No tienes acceso')
    await expect(diego.obtenerGrupo('grupo-demo-1001-a')).rejects.toThrow('tu cuenta')
  })
  it('distingue cero decisiones seguras de ausencia de datos', () => {
    expect(porcentajeSeguro({ decisiones_seguras: 0, decisiones_evaluadas: 5, completada: true })).toBe(0)
    expect(porcentajeSeguro({ decisiones_seguras: 0, decisiones_evaluadas: 0, completada: false })).toBeNull()
    expect(porcentajeSeguro({ decisiones_seguras: 8, decisiones_evaluadas: 5, completada: false })).toBeNull()
    expect(porcentajeSeguro({ decisiones_seguras: NaN, decisiones_evaluadas: 5, completada: false })).toBeNull()
  })
  it('presenta las tres temáticas, sin porcentajes ni medidores donde no hay datos', async () => {
    const r = await crearPanelDemo(1001).obtenerReporteNino(101)
    render(<Tematicas resultados={r.tematicas} />)
    expect(screen.getAllByRole('meter')).toHaveLength(2)
    const retos = screen.getByRole('region', { name: 'Retos Virales' })
    expect(within(retos).queryByRole('meter')).toBeNull()
    expect(retos.textContent).not.toContain('%')
    expect(retos.textContent).toContain('No hay datos disponibles')
  })
  it('un niño sin actividad no muestra ningún porcentaje', async () => {
    const r = await crearPanelDemo(1001).obtenerReporteNino(102)
    const { container } = render(<Tematicas resultados={r.tematicas} />)
    expect(screen.queryAllByRole('meter')).toHaveLength(0)
    expect(container.textContent).not.toContain('%')
  })
  it('el agregado pondera por número de decisiones y no devuelve datos personales', async () => {
    const r = await crearPanelDemo(1001).obtenerReporteGrupo('grupo-demo-1001-a')
    expect(r.tematicas[0].metricas?.decisiones_seguras).toBe(32)
    expect(r.tematicas[0].metricas?.decisiones_evaluadas).toBe(40)
    expect(JSON.stringify(r)).not.toMatch(/@|Martina|Florencia|Mateo|Antonia|usuario-|nino_id|miembros/)
    expect(Object.keys(r)).not.toContain('avance_miembros')
  })
  it('no agrega con menos de tres participantes y bloquea la exportación', async () => {
    const panel = crearPanelDemo(1001)
    const g = await panel.crearGrupo({ nombre: 'Grupo pequeño' })
    await panel.agregarUsuario(g.id, 'familia.rojas@example.com')
    const r = await panel.obtenerReporteGrupo(g.id)
    expect(r.tematicas.every(t => t.metricas === null)).toBe(true)
    expect(() => crearPdfGrupo(r)).toThrow('datos suficientes')
  })
  it('suprime por temática cuando el resto sí tiene una muestra suficiente', async () => {
    const panel = crearPanelDemo(1001)
    await panel.eliminarUsuario('grupo-demo-1001-a', 'usuario-903')
    const r = await panel.obtenerReporteGrupo('grupo-demo-1001-a')
    expect(r.tematicas[0].metricas).not.toBeNull()
    expect(r.tematicas[2].metricas).toBeNull()
    expect(r.tematicas[2].motivo).toBe('muestra_insuficiente')
  })
  it('el PDF es un archivo válido que solo exporta campos agregados', async () => {
    const r = await crearPanelDemo(1001).obtenerReporteGrupo('grupo-demo-1001-a')
    const contaminado = { ...r, miembros: [{ email: 'secreto@example.com', nombre: 'NombrePrivado' }], transcripcion: 'ConversacionPrivada' } as ReporteGrupo
    const doc = crearPdfGrupo(contaminado, true)
    const pdf = doc.output()
    expect(pdf.startsWith('%PDF-')).toBe(true)
    expect(doc.getNumberOfPages()).toBe(1)
    expect(pdf).toContain('/Subtype /Image')
    expect(pdf).toContain('/Width 2035')
    expect(pdf).toContain('/Height 1169')
    expect(pdf).toContain('/SMask')
    expect(pdf).toContain('Desconocidos')
    expect(pdf).toContain('Ciberacoso')
    expect(pdf).toContain('Retos Virales')
    expect(pdf).not.toMatch(/secreto@example|NombrePrivado|ConversacionPrivada|Martina|Florencia/)
    expect(pdf).toContain('80%')
  })
  it('un nuevo nivel actualiza el reporte individual y el agregado', async () => {
    const panel = crearPanelDemo(1001)
    const antes = await panel.obtenerReporteGrupo('grupo-demo-1001-a')
    await simularProgreso(1001, 101, 'retos_virales')
    const personal = await panel.obtenerReporteNino(101)
    const despues = await panel.obtenerReporteGrupo('grupo-demo-1001-a')
    expect(personal.tematicas[2].metricas?.decisiones_evaluadas).toBe(5)
    expect(despues.tematicas[2].metricas?.decisiones_evaluadas).toBe((antes.tematicas[2].metricas?.decisiones_evaluadas ?? 0) + 5)
  })
  it('el PDF distingue cero de temáticas suprimidas o ausentes sin inventar fortalezas', async () => {
    const r = await crearPanelDemo(1001).obtenerReporteGrupo('grupo-demo-1001-a')
    r.tematicas = [
      { tematica: 'desconocidos', metricas: { decisiones_seguras: 0, decisiones_evaluadas: 10, completada: true } },
      { tematica: 'ciberacoso', metricas: null, motivo: 'muestra_insuficiente' },
    ]
    const pdf = crearPdfGrupo(r).output()
    expect(pdf.match(/0% seguras/g)).toHaveLength(1)
    expect(pdf).toContain('Sin datos suficientes')
    expect(pdf).toContain('No hay datos disponibles')
    expect(pdf).toContain('no se registran decisiones seguras')
    expect(pdf).toContain('no hay un análisis de patrones disponible')
    expect(pdf).not.toMatch(/80%|75%|Infinity|NaN/)
  })
  it('el PDF no confunde un porcentaje redondeado con decisiones perfectas', async () => {
    const r = await crearPanelDemo(1001).obtenerReporteGrupo('grupo-demo-1001-a')
    r.tematicas = r.tematicas.map(t => ({ ...t, metricas: { decisiones_seguras: 999, decisiones_evaluadas: 1000, completada: true } }))
    const pdf = crearPdfGrupo(r).output()
    expect(pdf.match(/100% seguras/g)).toHaveLength(3)
    expect(pdf).toContain('100% redondeado')
    expect(pdf).not.toContain('Todas las decisiones evaluadas fueron seguras')
  })
})
