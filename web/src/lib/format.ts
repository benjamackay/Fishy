const FECHA = new Intl.DateTimeFormat('es-CL', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
})

const FECHA_HORA = new Intl.DateTimeFormat('es-CL', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function fecha(iso: string | null): string {
  if (!iso) return '-'
  return FECHA.format(new Date(iso))
}

export function fechaHora(iso: string | null): string {
  if (!iso) return '-'
  return FECHA_HORA.format(new Date(iso))
}

/** "hace 3 dias", para las tablas de actividad. */
export function haceCuanto(iso: string | null): string {
  if (!iso) return 'sin actividad'

  const dias = Math.floor((Date.now() - new Date(iso).getTime()) / 86400000)
  if (dias <= 0) return 'hoy'
  if (dias === 1) return 'ayer'
  if (dias < 30) return `hace ${dias} dias`
  return fecha(iso)
}

export function porcentaje(valor: number): string {
  return `${Math.round(valor)}%`
}

/**
 * El riesgo del backend viene con el signo invertido respecto de la intuicion:
 * **mas alto = mas seguro**. Se traduce a una etiqueta para no obligar al
 * adulto a interpretar un numero con signo.
 */
export function etiquetaRiesgo(total: number, respuestas: number): {
  texto: string
  tono: 'bien' | 'medio' | 'mal' | 'neutro'
} {
  if (respuestas === 0) return { texto: 'sin datos', tono: 'neutro' }
  const promedio = total / respuestas
  if (promedio >= 1) return { texto: 'decisiones seguras', tono: 'bien' }
  if (promedio >= 0) return { texto: 'mejorable', tono: 'medio' }
  return { texto: 'decisiones de riesgo', tono: 'mal' }
}
