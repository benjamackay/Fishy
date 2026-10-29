import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { ContextoSesion } from '@/auth/contexto'
import { ContextoPanelDemo } from '@/hooks/usePanel'
import { perfilesDemo } from './sesionDemo'
import { crearPanelPeriodo } from './reportesPeriodo'

export function ProveedorPeriodoDemo({ children }: { children: ReactNode }) {
  const [cuenta] = useState<'principal' | 'alternativa'>(() => {
    const elegido = new URLSearchParams(window.location.search).get('demoRol')
    if (elegido) return elegido === 'profesor' ? 'alternativa' : 'principal'
    try { return sessionStorage.getItem('fishy.periodo.demo.rol') === 'profesor' ? 'alternativa' : 'principal' } catch { return 'principal' }
  })
  useEffect(() => { try { sessionStorage.setItem('fishy.periodo.demo.rol', cuenta === 'alternativa' ? 'profesor' : 'familia') } catch { /* La demo también funciona sin almacenamiento. */ } }, [cuenta])
  const [dentro, setDentro] = useState(true)
  const perfil = dentro ? perfilesDemo[cuenta] : null
  const fuente = useMemo(() => crearPanelPeriodo(perfilesDemo[cuenta].id), [cuenta])
  return <ContextoSesion.Provider value={{ perfil, autenticado: dentro, cargando: false, esProfesor: perfil?.rol === 'profesor', entrar: async () => setDentro(true), salir: () => setDentro(false) }}>
    <ContextoPanelDemo.Provider value={fuente}><div className="demo-periodo-banner"><div><strong>Demostración · {perfil?.nombre ?? 'Cuenta de muestra'} · {cuenta === 'principal' ? 'Familia' : 'Profesor'}</strong><p>Datos ficticios de 2026: 05/10 y 15/10 con muestras suficientes; 18/10 con muestra parcial en Ciberacoso. Agosto sin decisiones evaluadas.</p></div><div className="pila">
      <a className="boton" href={cuenta === 'principal' ? '/?demoRol=profesor' : '/?demoRol=familia'}>Ver como {cuenta === 'principal' ? 'profesor' : 'familia'}</a>
      {!dentro && <button type="button" className="boton" onClick={() => setDentro(true)}>Entrar con cuenta de muestra</button>}
    </div></div>{children}</ContextoPanelDemo.Provider>
  </ContextoSesion.Provider>
}
