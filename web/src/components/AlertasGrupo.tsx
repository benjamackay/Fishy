import { TEMATICAS } from '@/types/reportes'
import type { TematicaId } from '@/types/reportes'
import { Icono } from './Icono'
import './alertas-grupo.css'

/** Indicador cualitativo: no calcula resultados ni hace consultas individuales. */
export function AlertaApoyo({ areas }: { areas: TematicaId[] }) {
  const temas = TEMATICAS.filter(t => areas.includes(t.id))
  if (!temas.length) return null
  return <span className="apoyo-indicador" role="note" aria-label={'Necesita apoyo en ' + temas.map(t => t.nombre).join(', ')}>
    <span className="apoyo-etiqueta"><Icono nombre="alerta" />Necesita apoyo</span>
    {temas.map(t => <span className="apoyo-area" key={t.id}><Icono nombre={t.icono} />{t.nombre}</span>)}
  </span>
}
