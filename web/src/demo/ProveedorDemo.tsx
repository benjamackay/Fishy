import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { ContextoSesion } from '@/auth/contexto'
import { ContextoPanelDemo } from '@/hooks/usePanel'
import { crearPanelDemo } from './gruposMock'
import { perfilesDemo } from './sesionDemo'

export function ProveedorDemo({ children }: { children: ReactNode }) {
  const [dentro, setDentro] = useState(true)
  const perfil = dentro ? perfilesDemo.principal : null
  const fuente = useMemo(() => crearPanelDemo(perfilesDemo.principal.id), [])
  return <ContextoSesion.Provider value={{ perfil, cargando: false, autenticado: dentro, esProfesor: false,
    entrar: async () => { setDentro(true) }, salir: () => { setDentro(false) } }}>
    <ContextoPanelDemo.Provider value={fuente}>
      <div className="demo-banner" role="status"><span><strong>Demostración del frontend</strong> · Cuenta ficticia: Camila Rojas · Sin conexión al backend</span>
        {!dentro && <button type="button" className="boton" onClick={() => setDentro(true)}>Entrar con cuenta de muestra</button>}
      </div>
      {children}
    </ContextoPanelDemo.Provider>
  </ContextoSesion.Provider>
}
