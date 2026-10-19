import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { errorPeriodo, etiquetaPeriodo } from '@/lib/periodo'
import type { Periodo } from '@/lib/periodo'
import { Icono } from './Icono'
import './filtro-periodo.css'

export function FiltroPeriodo({ periodo, cambiar, habilitado }: { periodo: Periodo | null; cambiar: (periodo: Periodo | null) => void; habilitado: boolean }) {
  const [inicio, setInicio] = useState(periodo?.inicio ?? '')
  const [fin, setFin] = useState(periodo?.fin ?? '')
  const [error, setError] = useState<string | null>(null)
  const primera = useRef<HTMLInputElement>(null)
  const ultima = useRef<HTMLInputElement>(null)
  const desplegable = useRef<HTMLDetailsElement>(null)
  const boton = useRef<HTMLElement>(null)
  useEffect(() => {
    const cerrar = (e: PointerEvent) => {
      if (desplegable.current && !desplegable.current.contains(e.target as Node)) desplegable.current.open = false
    }
    document.addEventListener('pointerdown', cerrar)
    return () => document.removeEventListener('pointerdown', cerrar)
  }, [])
  function aplicar(e: FormEvent) {
    e.preventDefault()
    const mensaje = errorPeriodo(inicio, fin)
    setError(mensaje)
    if (mensaje) { (!inicio || inicio > fin ? primera : ultima).current?.focus(); return }
    cambiar({ inicio, fin })
    if (desplegable.current) desplegable.current.open = false
    boton.current?.focus()
  }
  return <section className="periodo-card" aria-label="Filtrar reporte por período">
    <div className="periodo-aplicado" role="status"><span className="periodo-dot" /><span>{periodo ? etiquetaPeriodo(periodo) : 'Acumulado'}</span></div>
    {periodo && <button type="button" className="periodo-clear" onClick={() => { setInicio(''); setFin(''); setError(null); cambiar(null) }} aria-label="Eliminar filtro" title="Volver al acumulado"><Icono nombre="cerrar" /></button>}
    <details ref={desplegable} className="periodo-dropdown" onKeyDown={e => { if (e.key === 'Escape') { e.preventDefault(); if (desplegable.current) desplegable.current.open = false; boton.current?.focus() } }}>
      <summary ref={boton} role="button" aria-label="Filtrar por período"><Icono nombre="reloj" />{periodo ? 'Cambiar período' : 'Filtrar por período'}<span className="periodo-chevron" aria-hidden="true">⌄</span></summary>
      <div className="periodo-popover"><div className="periodo-heading"><h2>Elegir período</h2><p>Selecciona las fechas que quieres consultar.</p></div>
      <form onSubmit={aplicar} noValidate className="periodo-form">
      <label className="campo">Fecha inicial<input ref={primera} type="date" required value={inicio} disabled={!habilitado} onChange={e => { setInicio(e.target.value); setError(null) }} aria-invalid={!!error || undefined} aria-describedby={error ? 'periodo-error' : 'periodo-ayuda'} /></label>
      <label className="campo">Fecha final<input ref={ultima} type="date" required value={fin} disabled={!habilitado} onChange={e => { setFin(e.target.value); setError(null) }} aria-invalid={!!error || undefined} aria-describedby={error ? 'periodo-error' : 'periodo-ayuda'} /></label>
      <button className="boton boton--primario" disabled={!habilitado}>Aplicar filtro</button>
    </form>
    {error && <p id="periodo-error" role="alert" className="periodo-error">{error}</p>}
    <p id="periodo-ayuda" className="periodo-ayuda">{habilitado ? 'Incluye ambos días completos · Hora de Chile (America/Santiago).' : 'El filtro por período estará disponible al conectar las decisiones fechadas del servicio. Puedes probarlo en la demostración local.'}</p>
      </div>
    </details>
  </section>
}
