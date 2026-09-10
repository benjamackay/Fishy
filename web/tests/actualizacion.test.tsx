import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { EVENTO_DATOS, INTERVALO_REPORTES_MS, useDatosVivos } from '@/hooks/useDatosVivos'
import { ApiError } from '@/lib/api'
describe('actualización automática', () => {
  it('reconsulta por tiempo, foco, conexión, otra pestaña y eventos del juego', async () => {
    vi.useFakeTimers()
    let valor = 1
    const cargar = vi.fn(async () => valor)
    const { result, unmount } = renderHook(() => useDatosVivos(cargar, 'cuenta:1'))
    await act(async () => {})
    expect(result.current.datos).toBe(1)
    valor = 2
    await act(async () => { await vi.advanceTimersByTimeAsync(INTERVALO_REPORTES_MS) })
    expect(result.current.datos).toBe(2)
    for (const evento of ['focus', 'online', 'storage', EVENTO_DATOS]) {
      valor += 1
      await act(async () => { window.dispatchEvent(new Event(evento)) })
      expect(result.current.datos).toBe(valor)
    }
    const llamadas = cargar.mock.calls.length
    unmount()
    await act(async () => { await vi.advanceTimersByTimeAsync(INTERVALO_REPORTES_MS * 2); window.dispatchEvent(new Event('focus')) })
    expect(cargar.mock.calls.length).toBe(llamadas)
  })
  it('no consulta periódicamente una pestaña oculta y revalida al volver', async () => {
    vi.useFakeTimers()
    const cargar = vi.fn(async () => 1)
    renderHook(() => useDatosVivos(cargar, 'uno'))
    await act(async () => {})
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden')
    await act(async () => { await vi.advanceTimersByTimeAsync(INTERVALO_REPORTES_MS * 2) })
    expect(cargar).toHaveBeenCalledTimes(1)
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible')
    await act(async () => { document.dispatchEvent(new Event('visibilitychange')) })
    expect(cargar).toHaveBeenCalledTimes(2)
  })
  it('descarta una respuesta de la cuenta anterior y aborta su consulta', async () => {
    let resolver!: (v: string) => void
    let signalAnterior: AbortSignal | null = null
    const anterior = new Promise<string>(r => { resolver = r })
    const { result, rerender } = renderHook(({ cuenta }) => useDatosVivos(signal => {
      if (cuenta === 'a') { signalAnterior = signal; return anterior }
      return Promise.resolve('Datos B')
    }, cuenta), { initialProps: { cuenta: 'a' } })
    rerender({ cuenta: 'b' })
    await waitFor(() => expect(result.current.datos).toBe('Datos B'))
    await act(async () => { resolver('Privado A') })
    expect(result.current.datos).toBe('Datos B')
    expect(signalAnterior!.aborted).toBe(true)
  })
  it('un evento que llega durante una consulta obtiene los nuevos datos sin solapar requests', async () => {
    let resolver!: (v: number) => void
    const cargar = vi.fn().mockImplementationOnce(() => new Promise<number>(r => { resolver = r })).mockResolvedValue(2)
    const { result } = renderHook(() => useDatosVivos(cargar, 'uno'))
    await act(async () => { window.dispatchEvent(new Event(EVENTO_DATOS)); resolver(1) })
    await waitFor(() => expect(result.current.datos).toBe(2))
    expect(cargar).toHaveBeenCalledTimes(2)
  })
  it('conserva resultados con aviso ante un fallo temporal y los borra si se revoca acceso', async () => {
    const cargar = vi.fn().mockResolvedValueOnce('Datos').mockRejectedValueOnce(new Error('Red')).mockRejectedValueOnce(new ApiError(403, {}))
    const { result } = renderHook(() => useDatosVivos(cargar, 'uno'))
    await waitFor(() => expect(result.current.datos).toBe('Datos'))
    await act(async () => { window.dispatchEvent(new Event('focus')) })
    expect(result.current.datos).toBe('Datos')
    expect(result.current.error).not.toBeNull()
    await act(async () => { window.dispatchEvent(new Event('focus')) })
    expect(result.current.datos).toBeNull()
    expect(result.current.error).toBeInstanceOf(ApiError)
  })
})
