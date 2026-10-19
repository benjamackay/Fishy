import { useMemo } from 'react'
import { useSesion } from '@/auth/contexto'
import { panelReal } from '@/api/panelReal'
import { aplicarPermisosPanel } from '@/lib/permisosPanel'
import type { FuentePanel } from '@/types/panel'

export function usePanel(): FuentePanel {
  const { perfil } = useSesion()
  return useMemo(() => {
    return aplicarPermisosPanel(panelReal, perfil)
  }, [perfil])
}
