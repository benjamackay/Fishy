import { jsPDF } from 'jspdf'
import type { ReporteGrupo, TematicaId } from '@/types/reportes'
import { TEMATICAS } from '@/types/reportes'
import { hayResultados, porcentajeSeguro, tematicasCompletas } from './reportes'
import { ErrorUsuario } from './errores'
import { TAMANO_LOGO_TEXTO } from './marca'
import logoPdf from '@/assets/fishy-text-logo.png?inline'

type Color = [number, number, number]
const AZUL: Color = [55, 150, 224]
const OCRE: Color = [183, 113, 42]
const PRESENTACION: Record<TematicaId, { color: Color; detalle: string }> = {
  desconocidos: { color: AZUL, detalle: '(grooming)' },
  ciberacoso: { color: [246, 178, 31], detalle: '(exclusión digital)' },
  retos_virales: { color: OCRE, detalle: '(presión social)' },
}

function fechaPdf(valor: string | null): string {
  if (!valor || !Number.isFinite(Date.parse(valor))) return 'Fecha no disponible'
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  }).format(new Date(valor))
}

/** Adaptación grupal de reportedemo.pdf: solo campos agregados, nunca el DOM ni datos de miembros. */
export function crearPdfGrupo(reporte: ReporteGrupo, demo = false): jsPDF {
  if (reporte.participantes_con_resultados < Math.max(3, reporte.minimo_participantes) || !hayResultados(reporte.tematicas)) {
    throw new ErrorUsuario('Aún no hay datos suficientes para descargar este reporte.')
  }
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const margen = 14
  const ancho = 182
  doc.setProperties({ title: 'Fishy! - Reporte grupal de progreso', author: 'Fishy!', subject: 'Resumen agregado por temática', creator: 'Fishy!' })
  doc.setLineHeightFactor(1.25)

  function texto(contenido: string, y: number, tamano = 10.5, estilo = 'normal', centrado = false): number {
    doc.setFont('times', estilo)
    doc.setFontSize(tamano)
    doc.setTextColor(35, 35, 35)
    const lineas: string[] = doc.splitTextToSize(contenido, ancho)
    doc.text(lineas, centrado ? 105 : margen, y, { align: centrado ? 'center' : 'left' })
    return y + lineas.length * tamano * 0.3528 * 1.25
  }

  function dato(etiqueta: string, valor: string, y: number): number {
    doc.setFont('times', 'bold')
    doc.setFontSize(10.5)
    doc.setTextColor(35, 35, 35)
    doc.text(etiqueta, margen, y)
    const inicio = margen + doc.getTextWidth(etiqueta) + 1
    doc.setFont('times', 'normal')
    const lineas: string[] = doc.splitTextToSize(valor, 196 - inicio)
    doc.text(lineas, inicio, y)
    return y + lineas.length * 5
  }

  function recuadro(titulo: string, contenido: string, y: number, color: Color): number {
    doc.setFont('times', 'normal')
    doc.setFontSize(10.5)
    const lineas: string[] = doc.splitTextToSize(contenido, ancho - 8)
    const alto = 12 + lineas.length * 4.65
    doc.setFillColor(...color)
    doc.setDrawColor(...color)
    doc.setLineWidth(0.3)
    doc.roundedRect(margen, y, ancho, alto, 2.5, 2.5, 'FD')
    // El cuerpo blanco deja la cabecera de color y conserva las esquinas inferiores.
    doc.setFillColor(255, 255, 255)
    doc.roundedRect(margen + 0.3, y + 7, ancho - 0.6, alto - 7.3, 2.2, 2.2, 'F')
    doc.rect(margen + 0.3, y + 6, ancho - 0.6, 4, 'F')
    doc.setFont('times', 'bold')
    doc.setTextColor(255, 255, 255)
    doc.text(titulo, margen + 4, y + 4.4)
    doc.setFont('times', 'normal')
    doc.setTextColor(35, 35, 35)
    doc.text(lineas, margen + 4, y + 11)
    return y + alto
  }

  texto('Reporte de Progreso', 18, 24, 'bold')
  texto(demo ? 'Reporte grupal · Datos de demostración' : 'Reporte grupal', 24, 9)
  const anchoLogo = 28
  doc.addImage(logoPdf, 'PNG', 168, 9, anchoLogo, anchoLogo * TAMANO_LOGO_TEXTO.alto / TAMANO_LOGO_TEXTO.ancho, 'fishy-logo-texto', 'FAST')
  doc.setDrawColor(35, 35, 35)
  doc.setLineWidth(0.35)
  doc.line(margen, 28, 196, 28)

  let y = dato('Grupo:', reporte.nombre_grupo.replace(/\s+/g, ' ').trim().slice(0, 80), 37)
  y = dato('Integrantes:', String(reporte.total_integrantes), y)
  y = dato('Con resultados:', String(reporte.participantes_con_resultados), y)
  y = dato('Alcance del reporte:', 'Resultados acumulados disponibles', y)
  y = texto('Última actualización de los resultados: ' + fechaPdf(reporte.actualizado_en), y + 3, 9.5, 'italic', true)

  y = texto('Resumen por temática', y + 8, 15, 'bolditalic', true)
  y = texto('Solo métricas generales del grupo. Sin nombres, resultados individuales ni contenido literal de conversaciones.', y + 1, 9, 'italic', true)

  const temas = tematicasCompletas(reporte.tematicas)
  y += 6
  for (const tema of TEMATICAS) {
    const resultado = temas.find(r => r.tematica === tema.id)!
    const valor = porcentajeSeguro(resultado.metricas)
    const estilo = PRESENTACION[tema.id]
    doc.setTextColor(35, 35, 35)
    doc.setFont('times', 'bold')
    doc.setFontSize(11)
    doc.text(tema.nombre, 60, y + 3, { align: 'right' })
    doc.setFont('times', 'normal')
    doc.setFontSize(9)
    doc.text(estilo.detalle, 60, y + 7.5, { align: 'right' })
    if (valor === null) {
      doc.setTextColor(95, 95, 95)
      doc.text(resultado.motivo === 'muestra_insuficiente' ? 'Sin datos suficientes' : 'No hay datos disponibles', 65, y + 5)
    } else {
      doc.setFillColor(235, 238, 240)
      doc.roundedRect(65, y, 82, 6, 1, 1, 'F')
      if (valor > 0) {
        doc.setFillColor(...estilo.color)
        const relleno = 82 * valor / 100
        doc.roundedRect(65, y, relleno, 6, Math.min(1, relleno / 2), 1, 'F')
      }
      doc.setFont('times', 'bold')
      doc.setFontSize(11)
      doc.text(valor + '% seguras', 151, y + 4.5)
    }
    y += 12
  }
  y = texto('Decisiones seguras / decisiones evaluadas × 100, redondeado al entero. Se suman las decisiones del grupo por temática; sin datos suficientes no se muestran porcentajes.', y + 4, 9, 'italic', true)

  // Estos textos describen porcentajes observados, sin inferir conductas, emociones ni evolución.
  const disponibles = TEMATICAS.flatMap(tema => {
    const resultado = temas.find(r => r.tematica === tema.id)!
    const valor = porcentajeSeguro(resultado.metricas)
    return valor === null ? [] : [{ nombre: tema.nombre, valor }]
  })
  const mayor = Math.max(...disponibles.map(t => t.valor))
  const menor = Math.min(...disponibles.map(t => t.valor))
  const mejores = disponibles.filter(t => t.valor === mayor).map(t => t.nombre).join(' y ')
  const porReforzar = disponibles.filter(t => t.valor === menor).map(t => t.nombre).join(' y ')
  let fortaleza: string
  let mejora: string
  if (disponibles.length === 1) {
    fortaleza = `${mejores} registra ${mayor}% de decisiones seguras. Aún no hay otras temáticas con datos suficientes para comparar.`
    mejora = 'Completar las temáticas pendientes permitirá reconocer otras fortalezas y áreas para reforzar en el grupo.'
  } else if (mayor === menor) {
    fortaleza = `Las temáticas disponibles comparten un ${mayor}% de decisiones seguras en el grupo.`
    mejora = menor === 100
      ? 'Las temáticas disponibles alcanzan un 100% redondeado de decisiones seguras. Continuar practicando permitirá reforzar estos aprendizajes.'
      : 'Las temáticas disponibles tienen el mismo porcentaje. Se puede reforzar el aprendizaje en todas ellas.'
  } else {
    fortaleza = `${mejores}: ${mayor}% de decisiones seguras, el porcentaje más alto entre las temáticas disponibles.`
    mejora = `${porReforzar}: ${menor}% de decisiones seguras, el porcentaje más bajo entre las temáticas disponibles. Es una oportunidad para reforzar el aprendizaje del grupo.`
  }
  if (temas.every(t => !t.metricas || t.metricas.decisiones_seguras === 0)) fortaleza = 'Aún no se registran decisiones seguras en las temáticas disponibles. Se necesita más progreso para destacar una fortaleza.'

  y = texto('Fortalezas y áreas de mejora', y + 8, 15, 'bolditalic', true)
  y = recuadro('Fortaleza', fortaleza, y + 1, AZUL)
  y = recuadro('Área de mejora', mejora, y + 4, OCRE)
  y = texto('La comparación considera únicamente las temáticas con datos suficientes.', y + 6, 9, 'italic', true)

  y = texto('Patrones de progreso', y + 8, 15, 'bolditalic', true)
  y = texto('Aún no hay un análisis de patrones disponible. Los porcentajes muestran el resultado acumulado del grupo y no describen cómo cambian las decisiones a lo largo de una misión.', y + 2)

  const pie = Math.max(253, y + 5)
  doc.setDrawColor(110, 110, 110)
  doc.setLineWidth(0.25)
  doc.line(margen, pie, 196, pie)
  texto('Documento privado del grupo.', pie + 7, 9, 'bold', true)
  texto('Para el tutor administrador autorizado. No contiene datos personales ni resultados individuales de integrantes.', pie + 12, 9, 'normal', true)
  texto('Generado el ' + fechaPdf(new Date().toISOString()), pie + 19, 8, 'italic', true)
  return doc
}

export function descargarPdfGrupo(reporte: ReporteGrupo, demo = false): void {
  crearPdfGrupo(reporte, demo).save('Fishy-reporte-grupal-' + new Date().toISOString().slice(0, 10) + '.pdf')
}
