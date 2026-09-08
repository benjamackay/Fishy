import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { crearGrupo, GRUPOS_SON_MOCK, listarGrupos } from '@/api/grupos'
import { ErrorAviso, Vacio } from '@/components/Aviso'
import { Cargando } from '@/components/Cargando'
import { Kpi } from '@/components/datos'
import { useAsync } from '@/hooks/useAsync'
import { fecha } from '@/lib/format'

export default function GruposPage() {
  const cargar = useCallback(() => listarGrupos(), [])
  const { datos, cargando, error, recargar } = useAsync(cargar, [])

  const [nombre, setNombre] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const [creando, setCreando] = useState(false)
  const [errorCrear, setErrorCrear] = useState<Error | null>(null)

  async function alCrear(evento: React.FormEvent) {
    evento.preventDefault()
    setCreando(true)
    setErrorCrear(null)
    try {
      await crearGrupo({ nombre, descripcion })
      setNombre('')
      setDescripcion('')
      recargar()
    } catch (e: unknown) {
      setErrorCrear(e instanceof Error ? e : new Error(String(e)))
    } finally {
      setCreando(false)
    }
  }

  return (
    <>
      <div className="encabezado">
        <div>
          <h1>Grupos</h1>
          <p className="muted mini" style={{ margin: 0 }}>
            Junta perfiles de menores para seguir su avance individual y del
            grupo completo.
          </p>
        </div>
      </div>

      {GRUPOS_SON_MOCK && (
        <div className="aviso" style={{ marginBottom: '1.25rem' }}>
          <strong>Datos de prueba.</strong> El backend todavia no tiene grupos:
          esto corre contra un mock local que guarda en tu navegador. El contrato
          que Django debe implementar esta en <code>src/types/grupos.ts</code>;
          cuando exista, se cambia <code>VITE_GRUPOS_MOCK=false</code> y no hay
          que tocar ninguna pantalla.
        </div>
      )}

      <section className="card">
        <h2>Crear grupo</h2>
        <form onSubmit={alCrear} style={{ marginTop: '0.9rem' }}>
          <div
            style={{
              display: 'grid',
              gap: '0 1rem',
              gridTemplateColumns: 'repeat(auto-fit, minmax(14rem, 1fr))',
            }}
          >
            <label className="campo">
              <span>Nombre</span>
              <input
                value={nombre}
                onChange={(e) => setNombre(e.target.value)}
                placeholder="5to Basico A"
                required
              />
            </label>
            <label className="campo">
              <span>Descripcion (opcional)</span>
              <input
                value={descripcion}
                onChange={(e) => setDescripcion(e.target.value)}
                placeholder="Curso piloto del taller"
              />
            </label>
          </div>

          {errorCrear && (
            <div style={{ marginBottom: '0.9rem' }}>
              <ErrorAviso error={errorCrear} />
            </div>
          )}

          <button
            type="submit"
            className="boton boton--primario"
            disabled={creando || !nombre.trim()}
          >
            {creando ? 'Creando...' : 'Crear grupo'}
          </button>
        </form>
      </section>

      {cargando && <Cargando mensaje="Cargando grupos..." />}
      {error && <ErrorAviso error={error} onReintentar={recargar} />}

      {datos && (
        <>
          <div className="kpis" style={{ margin: '1.25rem 0' }}>
            <Kpi etiqueta="Grupos" valor={datos.length} />
            <Kpi
              etiqueta="Perfiles agrupados"
              valor={datos.reduce((t, g) => t + g.total_miembros, 0)}
            />
          </div>

          {datos.length === 0 ? (
            <div className="card">
              <Vacio>Todavia no hay grupos. Crea el primero arriba.</Vacio>
            </div>
          ) : (
            <div className="rejilla">
              {datos.map((grupo) => (
                <Link
                  key={grupo.id}
                  to={`/admin/grupos/${grupo.id}`}
                  className="ficha"
                >
                  <div className="ficha__titulo">{grupo.nombre}</div>
                  {grupo.descripcion && (
                    <p className="mini muted" style={{ margin: '0.3rem 0 0' }}>
                      {grupo.descripcion}
                    </p>
                  )}
                  <p className="mini muted" style={{ margin: '0.55rem 0 0' }}>
                    {grupo.total_miembros}{' '}
                    {grupo.total_miembros === 1 ? 'miembro' : 'miembros'} · creado
                    el {fecha(grupo.fecha_creacion)}
                  </p>
                </Link>
              ))}
            </div>
          )}
        </>
      )}
    </>
  )
}
