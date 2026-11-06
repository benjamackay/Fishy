import { expect, it } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ContextoSesion } from '@/auth/contexto'
import { ContextoPanelDemo } from '@/hooks/usePanel'
import { perfilesDemo } from '@/demo/sesionDemo'
import { crearPanelPeriodo, decisionesEnPeriodo } from '@/demo/reportesPeriodo'
import { aplicarPermisosPanel } from '@/lib/permisosPanel'
import { rutas } from '@/routes'

function abrir(profesor = false) {
  const perfil = perfilesDemo[profesor ? 'alternativa' : 'principal']
  const fuente = crearPanelPeriodo(perfil.id)
  const router = createMemoryRouter(rutas, { initialEntries: [profesor ? '/admin/grupos/grupo-demo-1002-a/reporte' : '/reportes/101'] })
  render(<ContextoSesion.Provider value={{ perfil, autenticado: true, cargando: false, esProfesor: profesor, entrar: async () => {}, salir: () => {} }}><ContextoPanelDemo.Provider value={fuente}><RouterProvider router={router} /></ContextoPanelDemo.Provider></ContextoSesion.Provider>)
  return userEvent.setup()
}
function fechas(inicio: string, fin: string) {
  const boton = screen.getByRole('button', { name: 'Filtrar por período' })
  if (!boton.closest('details')?.open) fireEvent.click(boton)
  fireEvent.change(screen.getByLabelText('Fecha inicial'), { target: { value: inicio } })
  fireEvent.change(screen.getByLabelText('Fecha final'), { target: { value: fin } })
}
async function porcentaje(valor: number) {
  await waitFor(() => {
    const medidores = screen.getAllByRole('meter')
    expect(medidores.length).toBe(3)
    medidores.forEach(m => expect(m.getAttribute('aria-valuenow')).toBe(String(valor)))
  })
}

it('incluye el día completo en Chile y calcula desde decisiones evaluadas', () => {
  const filas = decisionesEnPeriodo([101], { inicio: '2026-10-05', fin: '2026-10-05' })
  expect(filas).toHaveLength(3)
  expect(filas.every(f => f.registrada_en === '2026-10-05T23:59:59-03:00')).toBe(true)
  expect(decisionesEnPeriodo([101], { inicio: '2026-08-01', fin: '2026-08-01' })).toHaveLength(0)
})

it('aplica un rango, identifica sus fechas y restaura el acumulado de la familia', async () => {
  const user = abrir()
  await porcentaje(67)
  fechas('2026-10-15', '2026-10-15')
  await user.click(screen.getByRole('button', { name: 'Aplicar filtro' }))
  await porcentaje(100)
  expect(screen.getByRole('button', { name: 'Filtrar por período' }).closest('details')?.open).toBe(false)
  expect(screen.getByText('Del 15/10/2026 al 15/10/2026')).toBeTruthy()
  await user.click(screen.getByRole('button', { name: 'Eliminar filtro' }))
  await porcentaje(67)
  expect(screen.getByText('Acumulado')).toBeTruthy()
})

it('rechaza rangos incompletos o invertidos sin reemplazar el reporte', async () => {
  const user = abrir()
  await porcentaje(67)
  fechas('2026-10-15', '')
  await user.click(screen.getByRole('button', { name: 'Aplicar filtro' }))
  expect(screen.getByRole('alert').textContent).toContain('fecha inicial y la fecha final')
  await porcentaje(67)
  fechas('2026-10-15', '2026-10-05')
  await user.click(screen.getByRole('button', { name: 'Aplicar filtro' }))
  expect(screen.getByRole('alert').textContent).toContain('posterior')
  await porcentaje(67)
})

it('muestra Sin datos y ningún porcentaje cuando el período no tiene decisiones evaluadas', async () => {
  const user = abrir()
  await porcentaje(67)
  fechas('2026-08-01', '2026-08-31')
  await user.click(screen.getByRole('button', { name: 'Aplicar filtro' }))
  await waitFor(() => expect(screen.queryAllByRole('meter')).toHaveLength(0))
  expect(screen.getAllByText('Sin datos').length).toBeGreaterThan(0)
})

it('filtra el agregado del profesor y recupera su acumulado sin exponer menores', async () => {
  const user = abrir(true)
  await porcentaje(67)
  fechas('2026-10-05', '2026-10-05')
  await user.click(screen.getByRole('button', { name: 'Aplicar filtro' }))
  await porcentaje(0)
  expect((screen.getByRole('button', { name: 'Descargar reporte' }) as HTMLButtonElement).disabled).toBe(false)
  expect(screen.queryByText('Sofía')).toBeNull()
  await user.click(screen.getByRole('button', { name: 'Eliminar filtro' }))
  await porcentaje(67)
})

it('conserva las restricciones de rol en reportes por período', async () => {
  const profesor = aplicarPermisosPanel(crearPanelPeriodo(1002), perfilesDemo.alternativa)
  await expect(profesor.obtenerReporteNino(101, { periodo: { inicio: '2026-10-01', fin: '2026-10-31' } })).rejects.toThrow('solo tienen acceso a grupos')
  const familia = aplicarPermisosPanel(crearPanelPeriodo(1001), perfilesDemo.principal)
  await expect(familia.obtenerReporteGrupo('grupo-demo-1002-a')).rejects.toThrow('Solo los tutores administradores')
})
