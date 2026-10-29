export interface Periodo { inicio: string; fin: string }
export const demoPeriodo = import.meta.env.DEV && import.meta.env.MODE !== 'test' && import.meta.env.VITE_PERIODO_DEMO === 'true'
export function errorPeriodo(inicio: string, fin: string): string | null {
  if (!inicio || !fin) return 'Selecciona la fecha inicial y la fecha final.'
  const valida = (fecha: string) => /^\d{4}-\d{2}-\d{2}$/.test(fecha) && !Number.isNaN(Date.parse(fecha)) && new Date(fecha).toISOString().slice(0, 10) === fecha
  if (!valida(inicio) || !valida(fin)) return 'Ingresa fechas válidas.'
  return inicio > fin ? 'La fecha inicial no puede ser posterior a la fecha final.' : null
}
export function fechaChile(instante: string): string {
  const partes = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(instante))
  const parte = (tipo: string) => partes.find(p => p.type === tipo)!.value
  return parte('year') + '-' + parte('month') + '-' + parte('day')
}
export function etiquetaPeriodo(periodo: Periodo): string {
  const formato = (fecha: string) => fecha.split('-').reverse().join('/')
  return 'Del ' + formato(periodo.inicio) + ' al ' + formato(periodo.fin)
}
