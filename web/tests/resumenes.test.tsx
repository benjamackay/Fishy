import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { panelReal } from '@/api/panelReal'
import { api, ApiError } from '@/lib/api'
import { fechaActualizacion } from '@/lib/reportes'
import { aplicarPermisosPanel } from '@/lib/permisosPanel'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { rutas } from '@/routes'
import { prepararCuenta } from './fixtures/sesion'
import { perfilesDemo } from './fixtures/sesionDemo'
import type { ReporteNino } from '@/types/reportes'

const jugadores = [1, 2, 3].map(id => ({ id, adulto: 1001, nombre: 'Niño ' + id, edad: id - 1 }))
function reporte(id: number): ReporteNino {
  return { nino: { id, adulto_id: 1001, nombre: 'Niño ' + id, edad: id - 1, actualizado_en: null },
    actualizado_en: '2026-10-04T12:00:00Z',
    tematicas: [{ tematica: 'desconocidos', metricas: { decisiones_seguras: 0, decisiones_evaluadas: 5, completada: true } }] }
}

describe('resúmenes reales y datos incompletos', () => {
  it('consulta las métricas y fechas reales, conserva el cero y propaga la cancelación', async () => {
    const signal = new AbortController().signal
    const consultar = vi.spyOn(api, 'get').mockImplementation(async ruta => {
      if (ruta === '/jugadores/') return jugadores
      return reporte(Number(ruta.split('/')[2]))
    })
    const resultados = await panelReal.listarNinos({ signal })
    expect(resultados[0].tematicas?.[0].metricas?.decisiones_seguras).toBe(0)
    expect(resultados[0].actualizado_en).toBe('2026-10-04T12:00:00Z')
    expect(consultar).toHaveBeenCalledTimes(4)
    for (const llamada of consultar.mock.calls) expect(llamada[1]?.signal).toBe(signal)
  })
  it('un resumen fallido queda sin verificar, sin inventar ausencia de actividad', async () => {
    vi.spyOn(api, 'get').mockImplementation(async ruta => {
      if (ruta === '/jugadores/') return jugadores
      if (ruta === '/jugadores/2/reporte/') throw new ApiError(503, {})
      return reporte(Number(ruta.split('/')[2]))
    })
    const datos = await panelReal.listarNinos()
    expect(datos[1].tematicas).toBeUndefined()
    expect(datos[0].tematicas).toEqual(reporte(1).tematicas)
  })
  it('no conserva un resumen que corresponde a otra cuenta', async () => {
    const ajeno = reporte(1)
    ajeno.nino.adulto_id = 999
    vi.spyOn(api, 'get').mockImplementation(async ruta => ruta === '/jugadores/' ? [jugadores[0]] : ajeno)
    await expect(panelReal.listarNinos()).rejects.toThrow('a quién pertenece')
    await expect(aplicarPermisosPanel(panelReal, perfilesDemo.principal).obtenerReporteNino(1)).rejects.toThrow('No tienes acceso')
  })
  it('diferencia un resumen no disponible, una temática en cero y una sin resultados', async () => {
    prepararCuenta('principal')
    vi.spyOn(panelReal, 'listarNinos').mockResolvedValue([
      { ...reporte(1).nino, tematicas: reporte(1).tematicas },
      { ...reporte(2).nino },
      { ...reporte(3).nino, tematicas: [] },
    ])
    render(<ProveedorSesion><RouterProvider router={createMemoryRouter(rutas, { initialEntries: ['/'] })} /></ProveedorSesion>)
    await screen.findByText('Resumen no disponible.')
    expect(screen.getByText('0%')).toBeTruthy()
    expect(screen.getByText('Sin decisiones registradas')).toBeTruthy()
    expect(screen.getByText('Actualización no disponible')).toBeTruthy()
    expect(screen.getByText('0 años')).toBeTruthy()
    expect(screen.getByText('1 año')).toBeTruthy()
    expect(screen.getByText('—')).toBeTruthy()
  })
  it('una fecha ausente o inválida no se presenta como falta de actividad', () => {
    expect(fechaActualizacion(null)).toBe('Fecha no disponible')
    expect(fechaActualizacion('inválida')).toBe('Fecha no disponible')
    expect(fechaActualizacion('2025-05-01T12:00:00Z')).toContain('2025')
  })
})
