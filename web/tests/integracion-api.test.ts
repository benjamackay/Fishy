import { expect, it, vi } from 'vitest'
import { panelReal } from '@/api/panelReal'
import { guardarPerfil, cambiarContrasena, verificarRecuperacion } from '@/api/auth'

it('envía el período real y conserva la cancelación de la consulta', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ periodo: { inicio: '2026-10-05', fin: '2026-10-05' } })))
  vi.stubGlobal('fetch', fetch)
  const controller = new AbortController()
  await panelReal.obtenerReporteGrupo('grupo-real', { periodo: { inicio: '2026-10-05', fin: '2026-10-05' }, signal: controller.signal })
  expect(fetch).toHaveBeenCalledWith('/api/grupos/grupo-real/reporte/?inicio=2026-10-05&fin=2026-10-05', expect.objectContaining({ signal: expect.any(AbortSignal) }))
})

it('mapea el perfil y las contraseñas al contrato de Django', async () => {
  const fetch = vi.fn().mockImplementation(async () => new Response('{}'))
  vi.stubGlobal('fetch', fetch)
  await guardarPerfil({ nombre: 'familia', apellido: '', email: 'familia@example.com', fecha_nacimiento: null })
  expect(fetch).toHaveBeenLastCalledWith('/api/auth/perfil/', expect.objectContaining({ method: 'PATCH', body: JSON.stringify({ nombre: 'familia', apellido: '', email: 'familia@example.com', fecha_nacimiento: null }) }))
  await cambiarContrasena('Anterior123', 'NuevaClave123', 'NuevaClave123')
  expect(fetch).toHaveBeenLastCalledWith('/api/auth/cambiar-contrasena/', expect.objectContaining({ method: 'POST', body: JSON.stringify({ password_actual: 'Anterior123', password_nueva: 'NuevaClave123', password_confirmacion: 'NuevaClave123' }) }))
})

it('muestra errores del servidor sin aceptar un código rechazado', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ code: 'codigo_invalido', detail: 'El código no es correcto o ya venció.' }), { status: 400 })))
  await expect(verificarRecuperacion('solicitud', '012345')).rejects.toThrow('El código no es correcto o ya venció.')
})

// Reproduce lo que vio una persona en producción: /auth/cambiar-contrasena/ no
// estaba desplegado, Django devolvió su página HTML de 404 y el portal mostró
// el mensaje interno de ApiError, "La API respondió 404". Un apoderado o un
// profesor no puede hacer nada con ese texto.
it('no muestra el error técnico cuando el backend responde HTML en vez de JSON', async () => {
  const consola = vi.spyOn(console, 'error').mockImplementation(() => {})
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
    '<!doctype html><html><head><title>Page not found</title></head><body></body></html>',
    { status: 404, headers: { 'content-type': 'text/html' } },
  )))
  const error = await cambiarContrasena('Anterior123', 'NuevaClave123', 'NuevaClave123')
    .then(() => null, (e: Error) => e)
  expect(error).toBeInstanceOf(Error)
  expect(error!.message).not.toContain('La API respondió')
  expect(error!.message).toContain('todavía no está disponible')
  expect(consola.mock.calls[0][0]).toContain('no está desplegado')
  consola.mockRestore()
})

it('avisa de un servicio caído cuando el error 500 no trae JSON', async () => {
  const consola = vi.spyOn(console, 'error').mockImplementation(() => {})
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>502 Bad Gateway</html>', { status: 502 })))
  const error = await cambiarContrasena('Anterior123', 'NuevaClave123', 'NuevaClave123')
    .then(() => null, (e: Error) => e)
  expect(error!.message).toContain('no está respondiendo')
  expect(error!.message).not.toContain('La API respondió')
  consola.mockRestore()
})
