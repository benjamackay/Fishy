import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AgregarNinos } from '@/components/AgregarNinos'
import * as hooks from '@/hooks/usePanel'
import { panelReal } from '@/api/panelReal'
import { crearPanelDemo } from './fixtures/gruposMock'
import type { PerfilFamilia } from '@/types/grupos'

const perfiles: PerfilFamilia[] = [
  { jugador_id: 12, nombre: 'Martina', estado: 'disponible' },
  { jugador_id: 13, nombre: 'Tomás', estado: 'en_otro_curso' },
  { jugador_id: 14, nombre: 'Sofía', estado: 'en_este_curso' },
  { jugador_id: 15, nombre: 'Lucas', estado: 'disponible' },
]
const grupo = { id: 'curso', nombre: 'Curso', descripcion: '', fecha_creacion: '', total_miembros: 1, miembros: [], invitaciones: [] }
const json = (datos: unknown, status = 200) => new Response(JSON.stringify(datos), { status })
function abrir() {
  vi.spyOn(hooks, 'usePanel').mockReturnValue(panelReal)
  const guardar = vi.fn()
  const cerrar = vi.fn()
  const vista = render(<AgregarNinos id="curso" cerrar={cerrar} alGuardar={guardar} />)
  const user = userEvent.setup()
  return { ...vista, user, guardar, cerrar, buscar: async (correo = 'FAMILIA@example.com') => {
    await user.type(screen.getByLabelText('Correo del apoderado'), correo)
    await user.click(screen.getByRole('button', { name: 'Buscar' }))
  } }
}

describe('agregar por correo con el adaptador real', () => {
  it.each([200, 201])('envía POST con el mismo correo y solo los IDs elegidos; acepta %s', async status => {
    const enviar = vi.fn().mockResolvedValueOnce(json({ perfiles })).mockResolvedValueOnce(json(grupo, status))
    vi.stubGlobal('fetch', enviar)
    const { user, buscar, guardar } = abrir()
    await buscar()
    const otro = await screen.findByRole('checkbox', { name: /Tomás/ }) as HTMLInputElement
    const actual = screen.getByRole('checkbox', { name: /Sofía/ }) as HTMLInputElement
    expect(otro.disabled).toBe(true); expect(otro.checked).toBe(false)
    expect(actual.disabled).toBe(true); expect(actual.checked).toBe(true)
    expect((screen.getByRole('button', { name: 'Agregar', exact: true }) as HTMLButtonElement).disabled).toBe(true)
    await user.click(screen.getByRole('checkbox', { name: /Martina/ }))
    await user.click(screen.getByRole('checkbox', { name: /Lucas/ }))
    await user.click(screen.getByRole('button', { name: 'Agregar', exact: true }))
    await waitFor(() => expect(guardar).toHaveBeenCalledWith(grupo))
    expect(enviar.mock.calls.map(([url, options]) => [url, options.method, JSON.parse(options.body)])).toEqual([
      ['/api/grupos/curso/buscar-familia/', 'POST', { email: 'familia@example.com' }],
      ['/api/grupos/curso/miembros/', 'POST', { email: 'familia@example.com', jugador_ids: [12, 15] }],
    ])
    expect(enviar.mock.calls.every(([url]) => !url.includes('@'))).toBe(true)
  })
  it.each([
    [404, 'No encontramos perfiles de niño para ese correo.'],
    [403, 'Solo el profesor del curso puede consultar estos perfiles.'],
    [429, 'Demasiadas solicitudes. Inténtalo más tarde.'],
  ])('muestra el detalle del error %s sin reemplazarlo por datos ficticios', async (status, detail) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ detail }, status as number)))
    const { buscar, guardar } = abrir()
    await buscar()
    expect((await screen.findByRole('alert')).textContent).toContain(detail)
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0)
    expect(guardar).not.toHaveBeenCalled()
  })
  it('muestra errores 400 junto al campo de correo', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ email: ['Introduce una dirección válida.'] }, 400)))
    const { buscar } = abrir()
    await buscar()
    expect((await screen.findByRole('alert')).textContent).toContain('Introduce una dirección válida.')
    expect(screen.getByLabelText('Correo del apoderado').getAttribute('aria-invalid')).toBe('true')
  })
  it('muestra errores 400 de la selección y conserva la familia consultada', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(json({ perfiles })).mockResolvedValueOnce(json({ jugador_ids: ['Selecciona entre 1 y 20 niños.'] }, 400)))
    const { user, buscar, guardar } = abrir()
    await buscar()
    await user.click(await screen.findByRole('checkbox', { name: /Martina/ }))
    await user.click(screen.getByRole('button', { name: 'Agregar', exact: true }))
    expect((await screen.findByRole('alert')).textContent).toContain('Selecciona entre 1 y 20 niños.')
    expect(screen.getByText('familia@example.com')).toBeTruthy()
    expect(guardar).not.toHaveBeenCalled()
  })
  it('al recibir 409 vuelve a buscar, actualiza estados y descarta la selección anterior', async () => {
    const enviar = vi.fn().mockResolvedValueOnce(json({ perfiles }))
      .mockResolvedValueOnce(json({ detail: 'Martina ya está en otro curso. No se agregó ningún niño.' }, 409))
      .mockResolvedValueOnce(json({ perfiles: perfiles.map(p => p.jugador_id === 12 ? { ...p, estado: 'en_otro_curso' } : p) }))
    vi.stubGlobal('fetch', enviar)
    const { user, buscar, guardar } = abrir()
    await buscar()
    await user.click(await screen.findByRole('checkbox', { name: /Martina/ }))
    await user.click(screen.getByRole('checkbox', { name: /Lucas/ }))
    await user.click(screen.getByRole('button', { name: 'Agregar', exact: true }))
    await waitFor(() => expect(enviar).toHaveBeenCalledTimes(3))
    expect((await screen.findByRole('alert')).textContent).toContain('No se agregó ningún niño')
    expect((await screen.findByRole('checkbox', { name: /Martina/ }) as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByRole('checkbox', { name: /Lucas/ }) as HTMLInputElement).checked).toBe(false)
    expect((screen.getByRole('button', { name: 'Agregar', exact: true }) as HTMLButtonElement).disabled).toBe(true)
    expect(guardar).not.toHaveBeenCalled()
  })
  it('si la búsqueda tras un conflicto falla, impide reenviar los IDs antiguos', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(json({ perfiles }))
      .mockResolvedValueOnce(json({ detail: 'Martina ya está en otro curso.' }, 409)).mockRejectedValueOnce(new TypeError('Network error')))
    const { user, buscar, guardar } = abrir()
    await buscar()
    await user.click(await screen.findByRole('checkbox', { name: /Martina/ }))
    await user.click(screen.getByRole('button', { name: 'Agregar', exact: true }))
    expect(await screen.findByText(/No pudimos actualizar los perfiles/)).toBeTruthy()
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0)
    expect(screen.queryByRole('button', { name: 'Agregar', exact: true })).toBeNull()
    expect(guardar).not.toHaveBeenCalled()
  })
  it('cambiar el correo elimina los perfiles y la selección de la búsqueda anterior', async () => {
    const enviar = vi.fn().mockResolvedValueOnce(json({ perfiles })).mockResolvedValueOnce(json({ perfiles: [] }))
    vi.stubGlobal('fetch', enviar)
    const { user, buscar } = abrir()
    await buscar()
    await user.click(await screen.findByRole('checkbox', { name: /Martina/ }))
    await user.click(screen.getByRole('button', { name: 'Cambiar correo' }))
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0)
    await user.clear(screen.getByLabelText('Correo del apoderado'))
    await buscar('otra@example.com')
    expect((await screen.findByRole('alert')).textContent).toContain('No encontramos perfiles')
    expect(enviar).toHaveBeenCalledTimes(2)
  })
  it('bloquea los controles mientras busca y no envía dos búsquedas', async () => {
    let resolver!: (respuesta: Response) => void
    const enviar = vi.fn().mockImplementation(() => new Promise<Response>(resolve => { resolver = resolve }))
    vi.stubGlobal('fetch', enviar)
    const { user, buscar } = abrir()
    await buscar()
    expect((screen.getByLabelText('Correo del apoderado') as HTMLInputElement).disabled).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Procesando…' }))
    expect(enviar).toHaveBeenCalledTimes(1)
    resolver(json({ perfiles }))
    expect(await screen.findByRole('checkbox', { name: /Martina/ })).toBeTruthy()
  })
  it('limita la selección a 20 perfiles y permite corregirla', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ perfiles: Array.from({ length: 21 }, (_, i) => ({ jugador_id: i + 1, nombre: 'Niño ' + (i + 1), estado: 'disponible' })) })))
    const { user, buscar } = abrir()
    await buscar()
    const casillas = await screen.findAllByRole('checkbox') as HTMLInputElement[]
    for (const casilla of casillas.slice(0, 20)) await user.click(casilla)
    expect(casillas[20].disabled).toBe(true)
    await user.click(casillas[0])
    expect(casillas[20].disabled).toBe(false)
  })
})

describe('reglas de agregado en la demostración', () => {
  it('busca solo nombres/IDs/estados y agrega sin duplicar ni incorporar hermanos', async () => {
    const panel = crearPanelDemo(1002)
    const perfiles = (await panel.buscarFamilia('grupo-demo-1002-b', ' FAMILIA.SILVA@example.com ')).perfiles
    expect(perfiles).toEqual([
      { jugador_id: 103, nombre: 'Sofía', estado: 'en_otro_curso' },
      { jugador_id: 907, nombre: 'Valentina', estado: 'disponible' },
      { jugador_id: 908, nombre: 'Mateo', estado: 'disponible' },
    ])
    const primero = await panel.agregarNinos('grupo-demo-1002-b', 'familia.silva@example.com', [907])
    const segundo = await panel.agregarNinos('grupo-demo-1002-b', 'familia.silva@example.com', [907, 907])
    expect(primero.miembros).toEqual(segundo.miembros)
    expect(segundo.miembros.map(m => m.nombre_nino)).toEqual(['Valentina'])
    expect(segundo.invitaciones).toEqual([])
    await panel.eliminarUsuario(segundo.id, segundo.miembros[0].id)
    expect((await panel.buscarFamilia(segundo.id, 'familia.silva@example.com')).perfiles.find(p => p.jugador_id === 907)?.estado).toBe('disponible')
  })
  it('rechaza todo el agregado si un niño tiene otro curso o no pertenece al correo', async () => {
    const panel = crearPanelDemo(1002)
    const id = 'grupo-demo-1002-b'
    await expect(panel.agregarNinos(id, 'familia.silva@example.com', [907, 103])).rejects.toMatchObject({ status: 409 })
    await expect(panel.agregarNinos(id, 'familia.silva@example.com', [907, 904])).rejects.toMatchObject({ status: 404 })
    expect((await panel.obtenerGrupo(id)).miembros).toHaveLength(0)
    await expect(panel.buscarFamilia(id, 'nadie@example.com')).rejects.toMatchObject({ status: 404 })
    await expect(panel.agregarNinos(id, 'familia.silva@example.com', [])).rejects.toMatchObject({ status: 400 })
  })
})
