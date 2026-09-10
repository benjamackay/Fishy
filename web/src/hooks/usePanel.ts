import { useMemo } from 'react'
import { useSesion } from '@/auth/contexto'
import { panelReal } from '@/api/panelReal'
import { crearPanelDemo } from '@/mocks/gruposMock'
import { aplicarPermisosPanel } from '@/lib/permisosPanel'
import type { FuentePanel } from '@/types/panel'

export function usePanel(): FuentePanel {
  const { perfil, modoDemo } = useSesion()
  return useMemo(() => {
    const fuente = modoDemo ? crearPanelDemo(perfil?.id ?? -1) : panelReal
    return aplicarPermisosPanel(fuente, perfil)
  }, [perfil, modoDemo])
}
