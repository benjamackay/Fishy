import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { listarJugadores, listarPartidasDeJugador } from '@/api/jugadores'
import { ErrorAviso, Vacio } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { Kpi, Medidor } from '@/components/datos'
import { useAsync } from '@/hooks/useAsync'
import { fechaHora, haceCuanto, porcentaje } from '@/lib/format'
import type { Partida, UsuarioJugador } from '@/types/api'

interface PerfilConPartidas {
  jugador: UsuarioJugador
  partidas: Partida[]
}

/**
 * Panel del adulto: sus perfiles de menores y las partidas de cada uno.
 *
 * Se muestran agrupadas por perfil y no como una lista plana porque el progreso
 * cuelga del perfil: dos hermanos no comparten avance, y mezclarlos haria
 * ilegible de quien es cada partida.
 */
export default function SesionesPage() {
  const cargar = useCallback(async (): Promise<PerfilConPartidas[]> => {
    const jugadores = await listarJugadores()
    return Promise.all(
      jugadores.map(async (jugador) => ({
        jugador,
        partidas: await listarPartidasDeJugador(jugador.id),
      })),
    )
  }, [])

  const { datos, cargando, error, recargar } = useAsync(cargar, [])

  if (cargando) return <Cargando mensaje="Cargando tus perfiles..." />
  if (error) return <ErrorAviso error={error} onReintentar={recargar} />
  if (!datos) return null

  const partidas = datos.flatMap((p) => p.partidas)
  const promedio = partidas.length
    ? partidas.reduce((t, p) => t + p.progreso, 0) / partidas.length
    : 0
  const ultima = partidas
    .map((p) => p.fecha_update)
    .sort()
    .at(-1)

  return (
    <>
      <div className="encabezado">
        <div>
          <h1>Tus sesiones</h1>
          <p className="muted mini" style={{ margin: 0 }}>
            Cada perfil guarda su propio avance. Entra a una partida para ver el
            detalle.
          </p>
        </div>
      </div>

      <div className="kpis" style={{ marginBottom: '1.25rem' }}>
        <Kpi etiqueta="Perfiles" valor={datos.length} />
        <Kpi etiqueta="Partidas" valor={partidas.length} />
        <Kpi
          etiqueta="Progreso promedio"
          valor={porcentaje(promedio)}
          pie="entre todas las partidas"
        />
        <Kpi
          etiqueta="Ultima actividad"
          valor={ultima ? haceCuanto(ultima) : 'sin datos'}
        />
      </div>

      {datos.length === 0 && (
        <div className="card">
          <Vacio>
            Tu cuenta todavia no tiene perfiles de menores. Se crean desde el
            juego, al elegir quien va a jugar.
          </Vacio>
        </div>
      )}

      {datos.map(({ jugador, partidas: suyas }) => (
        <section className="card" key={jugador.id}>
          <div className="pila" style={{ justifyContent: 'space-between' }}>
            <h2>{jugador.nombre}</h2>
            <span className="mini muted">
              {jugador.edad ? `${jugador.edad} anos · ` : ''}
              {suyas.length} {suyas.length === 1 ? 'partida' : 'partidas'}
            </span>
          </div>

          {suyas.length === 0 ? (
            <Vacio>Este perfil todavia no ha empezado a jugar.</Vacio>
          ) : (
            <div className="rejilla" style={{ marginTop: '0.9rem' }}>
              {suyas.map((partida) => (
                <Link
                  key={partida.id}
                  to={`/partidas/${partida.id}`}
                  className="ficha"
                >
                  <div
                    className="pila"
                    style={{ justifyContent: 'space-between' }}
                  >
                    <span className="ficha__titulo">Partida #{partida.id}</span>
                    <span
                      className="mini muted"
                      style={{ fontVariantNumeric: 'tabular-nums' }}
                    >
                      {porcentaje(partida.progreso)}
                    </span>
                  </div>

                  <div style={{ margin: '0.6rem 0 0.5rem' }}>
                    <Medidor valor={partida.progreso} />
                  </div>

                  <div className="mini muted">
                    Actualizada {haceCuanto(partida.fecha_update)}
                  </div>
                  <div className="mini muted">
                    Inicio {fechaHora(partida.fecha_inicio)}
                  </div>
                </Link>
              ))}
            </div>
          )}
        </section>
      ))}
    </>
  )
}
