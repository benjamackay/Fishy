import { useCallback, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  agregarMiembro,
  eliminarGrupo,
  listarCandidatos,
  obtenerAvanceGrupo,
  obtenerGrupo,
  quitarMiembro,
} from '@/api/grupos'
import { ErrorAviso, Vacio } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { EstadoRiesgo, Kpi, Medidor } from '@/components/datos'
import { useAsync } from '@/hooks/useAsync'
import { haceCuanto, porcentaje } from '@/lib/format'
import type { AvanceGrupo, GrupoDetalle, MiembroCandidato } from '@/types/grupos'

interface Vista {
  grupo: GrupoDetalle
  avance: AvanceGrupo
  candidatos: MiembroCandidato[]
}

export default function GrupoPage() {
  const { id } = useParams<{ id: string }>()
  const grupoId = Number(id)
  const navegar = useNavigate()

  const [ocupado, setOcupado] = useState(false)
  const [seleccion, setSeleccion] = useState('')

  const cargar = useCallback(async (): Promise<Vista> => {
    const [grupo, avance, candidatos] = await Promise.all([
      obtenerGrupo(grupoId),
      obtenerAvanceGrupo(grupoId),
      listarCandidatos(grupoId),
    ])
    return { grupo, avance, candidatos }
  }, [grupoId])

  const { datos, cargando, error, recargar } = useAsync(cargar, [grupoId])

  async function conRecarga(accion: () => Promise<unknown>) {
    setOcupado(true)
    try {
      await accion()
      recargar()
    } finally {
      setOcupado(false)
    }
  }

  async function alAgregar(evento: React.FormEvent) {
    evento.preventDefault()
    if (!seleccion) return
    const jugadorId = Number(seleccion)
    setSeleccion('')
    await conRecarga(() => agregarMiembro(grupoId, jugadorId))
  }

  async function alEliminarGrupo() {
    if (!datos) return
    const confirmado = window.confirm(
      `Se eliminara el grupo "${datos.grupo.nombre}". Los perfiles de los menores no se tocan. ¿Continuar?`,
    )
    if (!confirmado) return
    await eliminarGrupo(grupoId)
    navegar('/admin/grupos', { replace: true })
  }

  if (cargando) return <Cargando mensaje="Cargando el grupo..." />
  if (error) return <ErrorAviso error={error} onReintentar={recargar} />
  if (!datos) return null

  const { grupo, avance, candidatos } = datos

  return (
    <>
      <div className="migas">
        <Link to="/admin/grupos">Grupos</Link> / {grupo.nombre}
      </div>

      <div className="encabezado">
        <div>
          <h1>{grupo.nombre}</h1>
          <p className="muted mini" style={{ margin: 0 }}>
            {grupo.descripcion || 'Sin descripcion'}
          </p>
        </div>
        <button
          type="button"
          className="boton boton--peligro"
          onClick={alEliminarGrupo}
        >
          Eliminar grupo
        </button>
      </div>

      <div className="kpis" style={{ marginBottom: '1.25rem' }}>
        <Kpi
          etiqueta="Progreso del grupo"
          valor={porcentaje(avance.progreso_promedio)}
          pie="promedio de los miembros"
        />
        <Kpi
          etiqueta="Miembros activos"
          valor={`${avance.miembros_con_actividad}/${avance.total_miembros}`}
          pie="han empezado a jugar"
        />
        <Kpi
          etiqueta="Misiones completadas"
          valor={avance.misiones_completadas}
          pie="sumadas del grupo"
        />
        <Kpi
          etiqueta="Oportunidades de mejora"
          valor={avance.oportunidades_mejora}
          pie="decisiones inseguras"
        />
      </div>

      <section className="card">
        <h2>Avance grupal</h2>
        <div style={{ margin: '0.75rem 0 0.5rem' }}>
          <Medidor valor={avance.progreso_promedio} />
        </div>
        <div className="pila" style={{ gap: '1rem' }}>
          <EstadoRiesgo
            total={avance.riesgo_total}
            respuestas={avance.respuestas}
          />
          <span className="mini muted">
            {avance.respuestas} respuestas del grupo ·{' '}
            {avance.zonas_completadas} zonas completadas
          </span>
        </div>
      </section>

      <section className="card">
        <div className="pila" style={{ justifyContent: 'space-between' }}>
          <h2>Miembros</h2>
          <span className="mini muted">
            {grupo.miembros.length}{' '}
            {grupo.miembros.length === 1 ? 'perfil' : 'perfiles'}
          </span>
        </div>

        <form
          onSubmit={alAgregar}
          className="pila"
          style={{ margin: '0.9rem 0 1rem', alignItems: 'flex-end' }}
        >
          <label className="campo" style={{ margin: 0, flex: '1 1 16rem' }}>
            <span>Agregar un perfil</span>
            <select
              value={seleccion}
              onChange={(e) => setSeleccion(e.target.value)}
              disabled={ocupado || candidatos.length === 0}
            >
              <option value="">
                {candidatos.length === 0
                  ? 'No quedan perfiles por agregar'
                  : 'Elige un perfil...'}
              </option>
              {candidatos.map((c) => (
                <option key={c.jugador_id} value={c.jugador_id}>
                  {c.nombre}
                  {c.edad ? ` (${c.edad})` : ''} — {c.adulto}
                </option>
              ))}
            </select>
          </label>
          <button
            type="submit"
            className="boton boton--primario"
            disabled={ocupado || !seleccion}
          >
            Agregar
          </button>
        </form>

        {grupo.miembros.length === 0 ? (
          <Vacio>El grupo no tiene miembros todavia.</Vacio>
        ) : (
          <div className="tabla-scroll">
            <table>
              <thead>
                <tr>
                  <th>Perfil</th>
                  <th>Responsable</th>
                  <th style={{ minWidth: '9rem' }}>Progreso</th>
                  <th className="num">Misiones</th>
                  <th className="num">Zonas</th>
                  <th>Decisiones</th>
                  <th className="num">A mejorar</th>
                  <th>Actividad</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {grupo.miembros.map((miembro) => {
                  const a = avance.avance_miembros.find(
                    (x) => x.jugador_id === miembro.jugador_id,
                  )
                  return (
                    <tr key={miembro.jugador_id}>
                      <td>
                        {miembro.nombre}
                        {miembro.edad && (
                          <span className="mini muted"> · {miembro.edad}</span>
                        )}
                      </td>
                      <td className="muted">{miembro.adulto}</td>
                      <td>
                        <div className="pila" style={{ flexWrap: 'nowrap' }}>
                          <span
                            className="mini"
                            style={{
                              minWidth: '2.6rem',
                              fontVariantNumeric: 'tabular-nums',
                            }}
                          >
                            {porcentaje(a?.progreso ?? 0)}
                          </span>
                          <span style={{ flex: 1, minWidth: '4rem' }}>
                            <Medidor valor={a?.progreso ?? 0} />
                          </span>
                        </div>
                      </td>
                      <td className="num">{a?.misiones_completadas ?? 0}</td>
                      <td className="num">{a?.zonas_completadas ?? 0}</td>
                      <td>
                        <EstadoRiesgo
                          total={a?.riesgo_total ?? 0}
                          respuestas={a?.respuestas ?? 0}
                        />
                      </td>
                      <td className="num">{a?.oportunidades_mejora ?? 0}</td>
                      <td className="muted mini">
                        {haceCuanto(a?.ultima_actividad ?? null)}
                      </td>
                      <td>
                        <button
                          type="button"
                          className="boton boton--peligro mini"
                          disabled={ocupado}
                          onClick={() =>
                            conRecarga(() =>
                              quitarMiembro(grupoId, miembro.jugador_id),
                            )
                          }
                        >
                          Quitar
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
