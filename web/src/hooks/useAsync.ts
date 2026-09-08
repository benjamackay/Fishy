import { useCallback, useEffect, useState } from 'react'
import type { DependencyList } from 'react'

export interface EstadoAsync<T> {
  datos: T | null
  cargando: boolean
  error: Error | null
  recargar: () => void
}

interface Resultado<T> {
  /** Deps + intento que produjeron este resultado. */
  clave: string
  datos: T | null
  error: Error | null
}

/**
 * Carga asincrona con estados de cargando/error y recarga manual.
 *
 * Se prefiere esto a traer react-query: el panel hace lecturas simples y sin
 * cache compartida entre pantallas, asi que no justifica la dependencia.
 *
 * `cargando` se DERIVA comparando la clave del resultado guardado con la clave
 * actual, en vez de setearse dentro del efecto. Asi cambiar de partida no
 * muestra por un frame los datos de la anterior, y no hay un setState sincrono
 * que dispare un render en cascada.
 *
 * La bandera `vigente` evita escribir estado despues de desmontar, que en
 * StrictMode (doble montaje en desarrollo) se nota enseguida.
 */
export function useAsync<T>(
  cargar: () => Promise<T>,
  deps: DependencyList,
): EstadoAsync<T> {
  const [intento, setIntento] = useState(0)
  const [resultado, setResultado] = useState<Resultado<T> | null>(null)

  const clave = `${JSON.stringify(deps)}|${intento}`

  useEffect(() => {
    let vigente = true

    cargar()
      .then((datos) => {
        if (vigente) setResultado({ clave, datos, error: null })
      })
      .catch((e: unknown) => {
        if (vigente) {
          setResultado({
            clave,
            datos: null,
            error: e instanceof Error ? e : new Error(String(e)),
          })
        }
      })

    return () => {
      vigente = false
    }
    // `cargar` se recrea en cada render, asi que la identidad la lleva `clave`.
  }, [clave]) // eslint-disable-line react-hooks/exhaustive-deps

  const recargar = useCallback(() => setIntento((n) => n + 1), [])

  const vigente = resultado?.clave === clave

  return {
    datos: vigente ? resultado.datos : null,
    cargando: !vigente,
    error: vigente ? resultado.error : null,
    recargar,
  }
}
