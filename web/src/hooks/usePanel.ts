import { createContext, useContext, useMemo } from 'react'
import { useSesion } from '@/auth/contexto'
import { panelReal } from '@/api/panelReal'
import { aplicarPermisosPanel } from '@/lib/permisosPanel'
import type { FuentePanel } from '@/types/panel'
export const ContextoPanelDemo = createContext<FuentePanel | null>(null)

export function usePanel(): FuentePanel {
  const { perfil } = useSesion()
  const demo = useContext(ContextoPanelDemo)
  return useMemo(() => {
    return aplicarPermisosPanel(demo ?? panelReal, perfil)
  }, [perfil, demo])
}

export function useEsDemoPeriodo() { return useContext(ContextoPanelDemo) !== null }
