import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { usePanel, useEsDemoPeriodo } from '@/hooks/usePanel'
import { useAsync } from '@/hooks/useAsync'
import { errorPeriodo, etiquetaPeriodo } from '@/lib/periodo'
import type { Periodo } from '@/lib/periodo'
import { ErrorUsuario } from '@/lib/errores'
import { TEMATICAS } from '@/types/reportes'
import { motivoIndisponible, numeroComparacion, porcentajeComparacion } from '@/lib/comparacion'
import { Cargando } from '@/components/Cargando'
import { ErrorAviso } from '@/components/Aviso'
import { Icono } from '@/components/Icono'
import { Privacidad } from '@/components/Reportes'
import './comparar-periodos.css'

type Rangos = { a: Periodo; b: Periodo }
const vacio = { inicio: '', fin: '' }

export default function CompararPeriodosPage() {
  const { id = '' } = useParams()
  const { perfil } = useSesion()
  const panel = usePanel()
  const demo = useEsDemoPeriodo()
  const [a, setA] = useState<Periodo>(vacio)
  const [b, setB] = useState<Periodo>(vacio)
  const [consulta, setConsulta] = useState<Rangos | null>(null)
  const [revision, setRevision] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const grupo = useAsync(() => panel.obtenerGrupo(id), [id, perfil?.id])
  const comparacion = useAsync(async () => {
    if (!consulta) return null
    // Comprueba nuevamente pertenencia antes de consultar ambos agregados.
    const acceso = await panel.obtenerGrupo(id)
    if (acceso.id !== id) throw new ErrorUsuario('No tienes acceso a este grupo.')
    const [primero, segundo] = await Promise.all([
      panel.obtenerReporteGrupo(id, { periodo: consulta.a }),
      panel.obtenerReporteGrupo(id, { periodo: consulta.b }),
    ])
    if (primero.grupo_id !== id || segundo.grupo_id !== id) throw new ErrorUsuario('No pudimos verificar a qué grupo pertenecen los resultados.')
    return { primero, segundo, rangos: consulta }
  }, [id, perfil?.id, consulta, revision])
  function comparar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setConsulta(null)
    const errorA = errorPeriodo(a.inicio, a.fin)
    const errorB = errorPeriodo(b.inicio, b.fin)
    if (errorA || errorB) { setError(`${errorA ? 'Período A: ' + errorA : 'Período B: ' + errorB}`); return }
    if (!grupo.datos || grupo.error) { setError('No se puede consultar la comparación de este grupo.'); return }
    setError(null); setConsulta({ a: { ...a }, b: { ...b } }); setRevision(v => v + 1)
  }
  const resultado = consulta && !grupo.error ? comparacion.datos : null
  const disponibles = resultado ? TEMATICAS.filter(t => porcentajeComparacion(resultado.primero.tematicas.find(r => r.tematica === t.id), resultado.primero) !== null && porcentajeComparacion(resultado.segundo.tematicas.find(r => r.tematica === t.id), resultado.segundo) !== null).length : 0
  return <div className="comparar-page">
    <nav className="migas" aria-label="Ruta de navegación"><Link to="/admin/grupos">Mis grupos</Link><span>/</span><Link to={'/admin/grupos/' + id + '/reporte'}>Reporte grupal</Link><span>/</span><span>Comparar períodos</span></nav>
    {grupo.cargando && <Cargando mensaje="Comprobando acceso al grupo…" />}
    {grupo.error && <ErrorAviso error={grupo.error} onReintentar={grupo.recargar} />}
    {grupo.datos && !grupo.error && <>
      <header className="comparar-heading"><div><span className="eyebrow">ANÁLISIS DEL GRUPO</span><h1>Dos períodos, una nueva perspectiva.</h1><p className="muted">{grupo.datos.nombre} · Compara las decisiones seguras de tu grupo por temática.</p></div><span className="comparar-icon"><Icono nombre="reportes" /></span></header>
      <section className="comparar-controls" aria-label="Elegir períodos para comparar">
        <form onSubmit={comparar} noValidate>
          <div className="comparar-rangos">{([{ clave: 'A', valor: a, cambiar: setA }, { clave: 'B', valor: b, cambiar: setB }] as const).map(({ clave, valor, cambiar }) => <fieldset key={clave} className={'comparar-rango rango-' + clave.toLowerCase()}><legend><span>{clave}</span>Período {clave}</legend><div>
            <label className="campo">Fecha inicial · {clave}<input type="date" required disabled={!!grupo.error} value={valor.inicio} onChange={e => cambiar({ ...valor, inicio: e.target.value })} aria-invalid={!!error || undefined} aria-describedby={error ? 'comparar-error' : 'comparar-ayuda'} /></label>
            <span className="comparar-separador" aria-hidden="true">→</span>
            <label className="campo">Fecha final · {clave}<input type="date" required disabled={!!grupo.error} value={valor.fin} onChange={e => cambiar({ ...valor, fin: e.target.value })} aria-invalid={!!error || undefined} aria-describedby={error ? 'comparar-error' : 'comparar-ayuda'} /></label>
          </div></fieldset>)}</div>
          <div className="comparar-actions"><p id="comparar-ayuda">Días completos en hora de Chile. Diferencia = período B menos período A.</p><button className="boton boton--primario" disabled={!!grupo.error || (!!consulta && comparacion.cargando)}><Icono nombre="reportes" />{consulta && comparacion.cargando ? 'Comparando…' : 'Comparar períodos'}</button></div>
          {error && <p className="aviso aviso--error" id="comparar-error" role="alert">{error}</p>}
        </form>
        {demo ? <div className="comparar-demo"><span>Prueba la muestra</span><button type="button" onClick={() => { setA({ inicio: '2026-10-05', fin: '2026-10-05' }); setB({ inicio: '2026-10-15', fin: '2026-10-15' }); setError(null) }}>Dos muestras suficientes</button><button type="button" onClick={() => { setA({ inicio: '2026-10-15', fin: '2026-10-15' }); setB({ inicio: '2026-10-18', fin: '2026-10-18' }); setError(null) }}>Una temática sin muestra suficiente</button></div> : <p className="mini muted">Se comparan las decisiones registradas en cada rango de fechas.</p>}
      </section>
      {consulta && comparacion.cargando && <Cargando mensaje="Comparando los dos períodos…" />}
      {consulta && comparacion.error && <ErrorAviso error={comparacion.error} onReintentar={comparacion.recargar} />}
      {resultado && <section aria-label="Resultados de la comparación" className="comparar-resultados">
        <div className="comparar-results-heading"><div><h2>El cambio, temática por temática</h2><p>{disponibles} de 3 temáticas con muestras suficientes en ambos períodos.</p></div><div className="comparar-legend"><span><i className="color-a" />Período A</span><span><i className="color-b" />Período B</span></div></div>
        <div className="comparar-cards">{TEMATICAS.map(t => {
          const ra = resultado.primero.tematicas.find(r => r.tematica === t.id)
          const rb = resultado.segundo.tematicas.find(r => r.tematica === t.id)
          const pa = porcentajeComparacion(ra, resultado.primero)
          const pb = porcentajeComparacion(rb, resultado.segundo)
          const diferencia = pa !== null && pb !== null ? Math.round((pb - pa) * 10) / 10 : null
          return <article className="comparar-topic" key={t.id} aria-labelledby={'comparar-' + t.id}>
            <div className="comparar-topic-title"><span className="topic-icon"><Icono nombre={t.icono} /></span><div><h3 id={'comparar-' + t.id}>{t.nombre}</h3><p>{t.descripcion}</p></div></div>
            <div className="comparar-chart" aria-label={'Gráfico de decisiones seguras: ' + t.nombre}>
              <div className="comparar-scale" aria-hidden="true"><span>0%</span><span>50%</span><span>100%</span></div>
              {([{ clave: 'A', valor: pa }, { clave: 'B', valor: pb }] as const).map(fila => <div className="comparar-bar-row" key={fila.clave}><span>{fila.clave}</span><div className="comparar-track">{fila.valor !== null && <div className={'comparar-bar color-' + fila.clave.toLowerCase()} style={{ width: fila.valor + '%' }} />}</div><strong>{fila.valor === null ? '—' : numeroComparacion(fila.valor) + '%'}</strong></div>)}
            </div>
            <div className="comparar-details">{([{ clave: 'A', valor: pa, datos: ra, reporte: resultado.primero, periodo: resultado.rangos.a }, { clave: 'B', valor: pb, datos: rb, reporte: resultado.segundo, periodo: resultado.rangos.b }] as const).map(fila => <div key={fila.clave} className={'comparar-detail detail-' + fila.clave.toLowerCase()}><span className="comparar-period-label">Período {fila.clave}</span><span className="comparar-dates">{etiquetaPeriodo(fila.periodo)}</span>{fila.valor === null ? <p className="comparar-unavailable">{motivoIndisponible(fila.datos, fila.reporte)}</p> : <p><strong>{numeroComparacion(fila.valor)}%</strong> · {fila.datos!.metricas!.decisiones_evaluadas} decisiones evaluadas<small>{fila.datos!.metricas!.decisiones_seguras} decisiones seguras</small></p>}</div>)}</div>
            <div className={'comparar-difference ' + (diferencia === null ? 'difference-unavailable' : diferencia > 0 ? 'difference-positive' : diferencia < 0 ? 'difference-negative' : 'difference-neutral')}>
              {diferencia === null ? <><Icono nombre="candado" /><div><strong>Comparación no disponible</strong><span>Sin diferencia numérica: falta una muestra suficiente en uno o ambos períodos.</span></div></> : <><span className="difference-number">{diferencia > 0 ? '+' : ''}{numeroComparacion(diferencia)}<small> pp</small></span><div><strong>{diferencia > 0 ? 'Aumento en decisiones seguras' : diferencia < 0 ? 'Disminución en decisiones seguras' : 'Sin variación'}</strong><span>Período B respecto del período A</span></div></>}
            </div>
          </article>
        })}</div><p className="comparar-footnote">Los porcentajes usan las decisiones seguras sobre las evaluadas en cada período. Las diferencias se expresan en puntos porcentuales (pp), no en crecimiento porcentual. Una muestra menor al mínimo se oculta para proteger a los participantes. Los rangos pueden coincidir o superponerse.</p>
        <Privacidad grupal />
      </section>}
      {!consulta && !error && <div className="comparar-empty"><Icono nombre="reportes" /><h2>Una mirada al antes y al después</h2><p>Elige dos rangos y compara para descubrir cómo cambian los resultados de tu grupo.</p></div>}
    </>}
  </div>
}
