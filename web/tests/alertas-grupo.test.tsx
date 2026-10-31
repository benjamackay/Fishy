import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AlertaApoyo } from '@/components/AlertasGrupo'

it('identifica apoyo y areas sin porcentajes ni consultas', () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const { container } = render(<AlertaApoyo areas={['desconocidos', 'retos_virales']} />)
  expect(screen.getByRole('note', { name: 'Necesita apoyo en Desconocidos, Retos Virales' })).toBeTruthy()
  expect(screen.getByText('Necesita apoyo')).toBeTruthy()
  expect(screen.getByText('Desconocidos')).toBeTruthy()
  expect(screen.getByText('Retos Virales')).toBeTruthy()
  expect(container.textContent).not.toContain('%')
  expect(fetch).not.toHaveBeenCalled()
})

it('no inventa un resultado positivo cuando no hay senales', () => {
  const { container } = render(<AlertaApoyo areas={[]} />)
  expect(container.textContent).toBe('')
  expect(screen.queryByRole('note')).toBeNull()
})

it('muestra cada area una sola vez', () => {
  render(<AlertaApoyo areas={['ciberacoso', 'ciberacoso']} />)
  expect(screen.getAllByText('Ciberacoso')).toHaveLength(1)
})
