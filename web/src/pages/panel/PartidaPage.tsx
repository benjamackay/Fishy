import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { obtenerJugador } from '@/api/jugadores'
import {
  listarInventario,
  listarMisiones,
  listarZonas,
  obtenerOportunidadesMejora,
  obtenerPartida,
  obtenerRiesgoPorZona,
} from '@/api/partidas'
import { ErrorAviso, Vacio } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { BarraRiesgoZona, EstadoRiesgo, Kpi, Medidor } from '@/components/datos'
import { useAsync } from '@/hooks/useAsync'
import { fecha, fechaHora, haceCuanto, porcentaje } from '@/lib/format'
import type {
  ItemInventario,
  MisionProgreso,
  OportunidadesMejora,
  Partida,
  RiesgoPorZona,
  UsuarioJugador,
  ZonaProgreso,
} from '@/types/api'

/**
 * Un endpoint secundario caido no debe dejar la pagina en blanco: el detalle se
 * arma con lo que si respondio. Solo la partida en si es obligatoria.
 */
async function opcional<T>(promesa: Promise<T>): Promise<T | null> {
  try {
    return await promesa
  } catch {
    return null
  }
}

interface Detalle {
  partida: Partida
  jugador: UsuarioJugador | null
  misiones: MisionProgreso[] | null
  zonas: ZonaProgreso[] | null
  inventario: ItemInventario[] | null
  riesgo: RiesgoPorZona | null
  oportunidades: OportunidadesMejora | null
}

export default function PartidaPage() {
  const { id } = useParams<{ id: string }>()
  const partidaId = Number(id)

  const cargar = useCallback(async (): Promise<Detalle> => {
    const partida = await obtenerPartida(partidaId)
    const [jugador, misiones, zonas, inventario, riesgo, oportunidades] =
      await Promise.all([
        opcional(obtenerJugador(partida.usuario_jugador)),
        opcional(listarMisiones(partidaId)),
        opcional(listarZonas(partidaId)),
        opcional(listarInventario(partidaId)),
        opcional(obtenerRiesgoPorZona(partidaId)),
        opcional(obtenerOportunidadesMejora(partidaId)),
      ])
    return { partida, jugador, misiones, zonas, inventario, riesgo, oportunidades }
  }, [partidaId])

  const { datos, cargando, error, recargar } = useAsync(cargar, [partidaId])

  if (cargando) return <Cargando mensaje="Cargando la partida..." />
  if (error) return <ErrorAviso error={error} onReintentar={recargar} />
  if (!datos) return null

  const { partida, jugador, misiones, zonas, inventario, riesgo, oportunidades } =
    datos

  const misionesCompletadas =
    misiones?.filter((m) => m.estado === 'completada').length ?? 0
  const zonasCompletadas = zonas?.filter((z) => z.completada).length ?? 0

  return (
    <>
      <div className="migas">
        <Link to="/">Tus sesiones</Link> / Partida #{partida.id}
      </div>

      <div className="encabezado">
        <div>
          <h1>{jugador ? jugador.nombre : `Partida #${partida.id}`}</h1>
          <p className="muted mini" style={{ margin: 0 }}>
            Partida #{partida.id} · iniciada el {fecha(partida.fecha_inicio)} ·
            actualizada {haceCuanto(partida.fecha_update)}
          </p>
        </div>
        <button type="button" className="boton" onClick={recargar}>
          Actualizar
        </button>
      </div>

      <div className="kpis" style={{ marginBottom: '1.25rem' }}>
        <Kpi etiqueta="Progreso" valor={porcentaje(partida.progreso)} />
        <Kpi
          etiqueta="Misiones completadas"
          valor={misionesCompletadas}
          pie={misiones ? `de ${misiones.length} desbloqueadas` : undefined}
        />
        <Kpi
          etiqueta="Zonas completadas"
          valor={zonasCompletadas}
          pie={zonas ? `de ${zonas.length} desbloqueadas` : undefined}
        />
        <Kpi
          etiqueta="Objetos en la mochila"
          valor={inventario?.reduce((t, i) => t + i.cantidad, 0) ?? '-'}
        />
      </div>

      <section className="card">
        <h2>Avance general</h2>
        <div style={{ margin: '0.75rem 0 0.4rem' }}>
          <Medidor valor={partida.progreso} />
        </div>
        <p className="mini muted" style={{ margin: 0 }}>
          {porcentaje(partida.progreso)} del recorrido del juego.
        </p>
      </section>

      <section className="card">
        <h2>Decisiones por zona</h2>
        {!riesgo ? (
          <Vacio>No pudimos cargar el riesgo por zona.</Vacio>
        ) : riesgo.zonas.length === 0 ? (
          <Vacio>
            Todavia no hay respuestas registradas en las zonas de riesgo.
          </Vacio>
        ) : (
          <>
            <p className="mini muted" style={{ marginTop: 0 }}>
              Mas a la derecha es mas seguro. La barra sale del cero: azul si las
              decisiones fueron seguras, rojo si predominaron las de riesgo.
            </p>

            <div
              style={{
                display: 'grid',
                gap: '1rem',
                margin: '1rem 0 0.75rem',
              }}
            >
              {riesgo.zonas.map((zona) => (
                <BarraRiesgoZona key={zona.zona} zona={zona} />
              ))}
            </div>

            <div className="pila" style={{ gap: '1rem' }}>
              <EstadoRiesgo
                total={riesgo.total}
                respuestas={riesgo.respuestas}
              />
              <span className="mini muted">
                {riesgo.respuestas}{' '}
                {riesgo.respuestas === 1
                  ? 'respuesta contabilizada'
                  : 'respuestas contabilizadas'}
                {riesgo.sin_clasificar > 0 &&
                  ` · ${riesgo.sin_clasificar} sin clasificar`}
              </span>
            </div>
          </>
        )}
      </section>

      <section className="card">
        <h2>Oportunidades de mejora</h2>
        <p className="mini muted" style={{ marginTop: 0 }}>
          Decisiones inseguras y la alternativa que se le escapo. Pensado para el
          adulto: el juego corrige por consecuencia narrativa, no señalando al
          menor.
        </p>

        {!oportunidades ? (
          <Vacio>No pudimos cargar las oportunidades de mejora.</Vacio>
        ) : oportunidades.oportunidades.length === 0 ? (
          <Vacio>Sin decisiones inseguras registradas. Buena senal.</Vacio>
        ) : (
          <ul className="lista-limpia" style={{ display: 'grid', gap: '0.9rem' }}>
            {oportunidades.oportunidades.map((o, indice) => (
              <li
                key={`${o.pregunta_banco_id}-${indice}`}
                style={{
                  paddingTop: indice === 0 ? 0 : '0.9rem',
                  borderTop:
                    indice === 0 ? 'none' : '1px solid var(--linea)',
                }}
              >
                <div className="pila" style={{ justifyContent: 'space-between' }}>
                  <strong style={{ fontSize: '0.92rem' }}>
                    {o.zona} · {o.npc}
                  </strong>
                  <span className="mini muted">{fechaHora(o.fecha)}</span>
                </div>

                <p className="mini muted" style={{ margin: '0.35rem 0' }}>
                  &ldquo;{o.mensaje_npc}&rdquo;
                </p>

                <div className="mini" style={{ marginTop: '0.4rem' }}>
                  <span className="estado estado--mal">Eligio</span>{' '}
                  {o.eligio.texto}
                </div>
                <div className="mini" style={{ marginTop: '0.25rem' }}>
                  <span className="estado estado--bien">Mejor opcion</span>{' '}
                  {o.mejor_opcion.texto}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="card">
        <h2>Misiones</h2>
        {!misiones ? (
          <Vacio>No pudimos cargar las misiones.</Vacio>
        ) : misiones.length === 0 ? (
          <Vacio>Todavia no hay misiones desbloqueadas.</Vacio>
        ) : (
          <div className="tabla-scroll">
            <table>
              <thead>
                <tr>
                  <th>Mision</th>
                  <th>Zona</th>
                  <th>Estado</th>
                  <th>Desbloqueada</th>
                  <th>Completada</th>
                </tr>
              </thead>
              <tbody>
                {misiones.map((m) => (
                  <tr key={m.id}>
                    <td>
                      {m.nombre || m.mision_id}
                      {!m.en_catalogo && (
                        <span className="mini muted"> (fuera del catalogo)</span>
                      )}
                    </td>
                    <td className="muted">{m.zona || '-'}</td>
                    <td>
                      <span
                        className={`estado estado--${
                          m.estado === 'completada' ? 'bien' : 'medio'
                        }`}
                      >
                        {m.estado === 'completada' ? 'completada' : 'en curso'}
                      </span>
                    </td>
                    <td className="muted">{fecha(m.fecha_desbloqueo)}</td>
                    <td className="muted">{fecha(m.fecha_completada)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div
        style={{
          display: 'grid',
          gap: '1rem',
          gridTemplateColumns: 'repeat(auto-fit, minmax(18rem, 1fr))',
          marginTop: '1rem',
        }}
      >
        <section className="card" style={{ marginTop: 0 }}>
          <h2>Zonas</h2>
          {!zonas ? (
            <Vacio>No pudimos cargar las zonas.</Vacio>
          ) : zonas.length === 0 ? (
            <Vacio>Sin zonas desbloqueadas.</Vacio>
          ) : (
            <ul className="lista-limpia" style={{ display: 'grid', gap: '0.5rem' }}>
              {zonas.map((z) => (
                <li
                  key={z.id}
                  className="pila"
                  style={{ justifyContent: 'space-between' }}
                >
                  <span>{z.zona}</span>
                  <span
                    className={`estado estado--${z.completada ? 'bien' : 'medio'}`}
                  >
                    {z.completada ? 'completada' : 'en curso'}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="card" style={{ marginTop: 0 }}>
          <h2>Mochila</h2>
          {!inventario ? (
            <Vacio>No pudimos cargar el inventario.</Vacio>
          ) : inventario.length === 0 ? (
            <Vacio>La mochila esta vacia.</Vacio>
          ) : (
            <div className="tabla-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Objeto</th>
                    <th className="num">Cantidad</th>
                  </tr>
                </thead>
                <tbody>
                  {inventario.map((item) => (
                    <tr key={item.id}>
                      <td>{item.item_id}</td>
                      <td className="num">{item.cantidad}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>
    </>
  )
}
