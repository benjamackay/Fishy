import { Navigate, useParams } from 'react-router-dom'
import { obtenerPartida } from '@/api/partidas'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useAsync } from '@/hooks/useAsync'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso } from '@/components/Aviso'
import { ErrorUsuario } from '@/lib/errores'

/** Compatibilidad de enlaces antiguos. Ningún endpoint de conversaciones se consulta. */
export default function PartidaPage() {
  const { id } = useParams()
  const { modoDemo, perfil } = useSesion()
  const panel = usePanel()
  const { datos, error, cargando, recargar } = useAsync(async () => {
    if (modoDemo) throw new ErrorUsuario('En la demostración, elige un reporte desde el panel.')
    const partida = await obtenerPartida(Number(id))
    const ninos = await panel.listarNinos()
    if (!ninos.some(n => n.id === partida.usuario_jugador)) throw new ErrorUsuario('No tienes acceso a este reporte.')
    return partida.usuario_jugador
  }, [id, perfil?.id, modoDemo])
  if (cargando) return <Cargando />
  if (error) return <ErrorAviso error={error} onReintentar={recargar} />
  return datos ? <Navigate to={'/reportes/' + datos} replace /> : null
}
