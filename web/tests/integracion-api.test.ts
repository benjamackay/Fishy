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
