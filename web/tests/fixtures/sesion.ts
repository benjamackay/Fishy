import { vi } from 'vitest'
import * as auth from '@/api/auth'
import { panelReal } from '@/api/panelReal'
import { crearPanelDemo } from './gruposMock'
import { perfilesDemo } from './sesionDemo'

/** Datos aislados en pruebas; las pantallas usan el proveedor de sesión real. */
export function prepararCuenta(cuenta: keyof typeof perfilesDemo, restaurar = true) {
  const perfil = perfilesDemo[cuenta]
  if (restaurar) localStorage.setItem('fishy.token', 'token-de-prueba')
  vi.spyOn(auth, 'obtenerPerfil').mockResolvedValue(perfil)
  vi.spyOn(auth, 'login').mockImplementation(async () => {
    localStorage.setItem('fishy.token', 'token-de-prueba')
    return { token: 'token-de-prueba', adulto_id: perfil.id }
  })
  const fuente = crearPanelDemo(perfil.id)
  for (const operacion of Object.keys(panelReal) as Array<keyof typeof panelReal>) {
    vi.spyOn(panelReal, operacion).mockImplementation(fuente[operacion] as never)
  }
}
