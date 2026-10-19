/**
 * Piezas de visualizacion. Nada de librerias de charts: todo lo que se muestra
 * son magnitudes sueltas, medidores y una barra divergente, y eso se dibuja
 * mejor con dos divs que con un runtime de 90 KB.
 */

import { etiquetaRiesgo, porcentaje } from '@/lib/format'
import type { RiesgoZona } from '@/types/api'

/** Cifra suelta. Valor en figuras proporcionales (no tabulares, es un titular). */
export function Kpi({
  etiqueta,
  valor,
  pie,
}: {
  etiqueta: string
  valor: string | number
  pie?: string
}) {
  return (
    <div className="kpi">
      <div className="kpi__etiqueta">{etiqueta}</div>
      <div className="kpi__valor">{valor}</div>
      {pie && <div className="kpi__pie">{pie}</div>}
    </div>
  )
}

/** Magnitud 0-100 en un solo tono, con la pista en un paso mas claro. */
export function Medidor({ valor }: { valor: number }) {
  const acotado = Math.max(0, Math.min(100, valor))
  return (
    <div
      className="medidor"
      role="meter"
      aria-valuenow={Math.round(acotado)}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={`Progreso ${porcentaje(acotado)}`}
    >
      <div className="medidor__relleno" style={{ width: `${acotado}%` }} />
    </div>
  )
}

/**
 * Estado de riesgo: color + SIEMPRE la etiqueta de texto. El color nunca carga
 * el significado solo.
 */
export function EstadoRiesgo({
  total,
  respuestas,
}: {
  total: number
  respuestas: number
}) {
  const { texto, tono } = etiquetaRiesgo(total, respuestas)
  return <span className={`estado estado--${tono}`}>{texto}</span>
}

/**
 * Riesgo de una zona sobre su escala posible.
 *
 * Es divergente porque tiene polaridad y un cero con significado: la barra sale
 * del cero hacia la derecha (azul, seguro) o hacia la izquierda (rojo, riesgo).
 * El ancho de la pista es el rango [minimo_posible, maximo_posible] de esas
 * mismas preguntas, para que el numero se lea contra lo que era alcanzable en
 * vez de flotar solo.
 */
export function BarraRiesgoZona({ zona }: { zona: RiesgoZona }) {
  const min = Math.min(zona.minimo_posible, 0)
  const max = Math.max(zona.maximo_posible, 0)
  const rango = max - min || 1

  const posicion = (valor: number) => ((valor - min) / rango) * 100
  const cero = posicion(0)
  const actual = posicion(zona.riesgo_acumulado)

  const positivo = zona.riesgo_acumulado >= 0
  const izquierda = Math.min(cero, actual)
  const ancho = Math.abs(actual - cero)

  return (
    <div>
      <div className="pila" style={{ justifyContent: 'space-between' }}>
        <strong style={{ fontSize: '0.9rem' }}>{zona.zona}</strong>
        <span className="mini muted" style={{ fontVariantNumeric: 'tabular-nums' }}>
          {zona.riesgo_acumulado > 0 ? '+' : ''}
          {zona.riesgo_acumulado} de {zona.maximo_posible} · {zona.respuestas}{' '}
          {zona.respuestas === 1 ? 'respuesta' : 'respuestas'}
        </span>
      </div>

      <div
        className="diverge"
        style={{ marginTop: '0.35rem' }}
        title={`${zona.zona}: ${zona.riesgo_acumulado} puntos sobre una escala de ${zona.minimo_posible} a ${zona.maximo_posible}, en ${zona.respuestas} respuestas.`}
      >
        <div className="diverge__cero" style={{ left: `${cero}%` }} />
        <div
          className="diverge__barra"
          style={{
            left: `${izquierda}%`,
            width: `${Math.max(ancho, 0.6)}%`,
            background: positivo ? 'var(--polo-seguro)' : 'var(--polo-riesgo)',
          }}
        />
      </div>
    </div>
  )
}
