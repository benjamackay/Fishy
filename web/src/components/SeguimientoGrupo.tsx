import { useState } from 'react'
import { useSesion } from '@/auth/contexto'
import { usePanel } from '@/hooks/usePanel'
import { useDatosVivos } from '@/hooks/useDatosVivos'
import { simularSeguimiento } from '@/mocks/gruposMock'
import { correoSeguimiento, etiquetasSeguimiento, nombreTema, requiereApoyo } from '@/lib/seguimiento'
import { fechaActualizacion } from '@/lib/reportes'
import { comoError } from '@/lib/errores'
import { ErrorAviso } from './Aviso'
import { Cargando } from './Cargando'
import { Icono } from './Icono'
import type { MiembroGrupo } from '@/types/grupos'
import type { AlumnoSeguimiento, CriteriosSeguimiento, TemaSeguimiento } from '@/types/seguimiento'
import './seguimiento.css'

const porcentaje = (n: number) => n.toLocaleString('es-CL', { maximumFractionDigits: 1 }) + '%'
const sugerencias = {
  desconocidos: 'Practicar cómo reconocer contactos desconocidos y pedir ayuda a una persona de confianza.',
  ciberacoso: 'Revisar cómo pedir ayuda, bloquear y reportar situaciones de ciberacoso.',
  retos_virales: 'Conversar sobre cómo evaluar riesgos antes de participar en un reto viral.',
}

function Tema({ tema: t, criterios: c }: { tema: TemaSeguimiento; criterios: CriteriosSeguimiento }) {
  return <li className={'support-topic support-topic--' + t.estado}>
    <strong>{nombreTema(t.tematica)}</strong>
    {t.porcentaje_seguro === null ? <>
      <span>{t.decisiones_evaluadas ? 'Aún no hay datos suficientes' : 'Sin datos disponibles'}</span>
      <small>{t.decisiones_evaluadas ? `${t.decisiones_evaluadas} de ${c.minimo_decisiones} decisiones mínimas. Todavía no se evalúa esta temática.` : 'Aún no se registran decisiones evaluables en esta temática.'}</small>
    </> : <>
      <span><b>{porcentaje(t.porcentaje_seguro)}</b> de decisiones seguras</span>
      <small>{t.decisiones_seguras} de {t.decisiones_evaluadas} decisiones evaluadas. {requiereApoyo(t.estado) ? `Por debajo del ${t.estado === 'prioritario' ? c.umbral_prioridad : c.umbral_apoyo}% de referencia.` : 'Sin alerta en esta temática.'}</small>
    </>}
    {t.actualizado_en && <small>Última decisión: {fechaActualizacion(t.actualizado_en)}</small>}
    {t.datos_antiguos && <small className="support-history">Incluye datos de hace más de {c.dias_datos_antiguos} días. Verificar el aprendizaje actual.</small>}
  </li>
}

function Alumno({ alumno: a, criterios: c, grupo }: { alumno: AlumnoSeguimiento; criterios: CriteriosSeguimiento; grupo: string }) {
  const apoyo = requiereApoyo(a.estado)
  const enlace = correoSeguimiento(a, grupo)
  const temasApoyo = a.tematicas.filter(t => requiereApoyo(t.estado))
  return <article className={'support-student support-student--' + a.estado} aria-label={'Seguimiento de ' + a.nombre_nino}>
    <div className="support-student-heading"><div><h3>{a.nombre_nino}</h3><span className="mini muted">{a.email_familia}</span></div><span className={'support-badge support-badge--' + a.estado}>{etiquetasSeguimiento[a.estado]}</span></div>
    {a.general ? <p className="support-general"><strong>Resumen de las temáticas evaluadas: {porcentaje(a.general.porcentaje_seguro)} seguro.</strong> {a.general.decisiones_seguras} de {a.general.decisiones_evaluadas} decisiones en {a.general.tematicas_evaluadas} de 3 temáticas. {requiereApoyo(a.general.estado) ? 'El conjunto también requiere refuerzo.' : apoyo ? 'El promedio no oculta las dificultades por temática.' : ''}</p> : <p className="support-general muted">Aún no hay suficientes decisiones en {c.minimo_tematicas_general} temáticas para un resumen general.</p>}
    <ul className="support-topics">{a.tematicas.map(t => <Tema key={t.tematica} tema={t} criterios={c} />)}</ul>
    {apoyo && <div className="support-next"><div><strong>Siguiente paso sugerido</strong><ul>{temasApoyo.map(t => <li key={t.tematica}>{sugerencias[t.tematica]}</li>)}</ul><p className="mini muted">Coordinar con la familia un acompañamiento y revisar las próximas decisiones del juego.</p></div>
      {enlace ? <a className="boton" href={enlace}><Icono nombre="correo" />Preparar correo a la familia</a> : <p className="mini muted">No hay un correo válido para preparar el contacto con la familia.</p>}
    </div>}
  </article>
}

export function SeguimientoGrupo({ id, miembros }: { id: string; miembros: MiembroGrupo[] }) {
  const { perfil, modoDemo } = useSesion()
  const panel = usePanel()
  const vinculos = miembros.map(m => m.id).sort().join(',')
  const estado = useDatosVivos(signal => panel.obtenerSeguimientoGrupo(id, { signal }), 'seguimiento:' + perfil?.id + ':' + modoDemo + ':' + id + ':' + vinculos)
  const [filtro, setFiltro] = useState<'apoyo' | 'todos' | 'pendientes'>('apoyo')
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [aviso, setAviso] = useState('')
  const datos = estado.datos
  // Un vínculo eliminado no puede quedar visible en una respuesta anterior.
  const ids = new Set(miembros.map(m => m.id))
  const alumnos = datos?.alumnos.filter(a => ids.has(a.miembro_id)) ?? []
  const apoyo = alumnos.filter(a => requiereApoyo(a.estado))
  const pendientes = alumnos.filter(a => a.estado === 'sin_datos' || a.estado === 'muestra_insuficiente')
  const visibles = filtro === 'apoyo' ? apoyo : filtro === 'pendientes' ? pendientes : alumnos
  async function simular(mejora: boolean) {
    if (!perfil || !modoDemo || ocupado) return
    setOcupado(true); setError(null); setAviso('')
    try {
      await simularSeguimiento(perfil.id, id, mejora)
      setAviso(mejora ? 'Mejora ficticia registrada. El seguimiento se vuelve a calcular automáticamente.' : 'Resultados ficticios registrados. El seguimiento se vuelve a calcular automáticamente para los alumnos de este grupo.')
    } catch (e) { setError(comoError(e)) } finally { setOcupado(false) }
  }
  return <section className="card support-panel" id="seguimiento" aria-labelledby="support-title">
    <div className="support-heading"><div><span className="eyebrow">ACOMPAÑAMIENTO DEL CURSO</span><h2 id="support-title">Alumnos que necesitan apoyo</h2><p className="mini muted">Identifica qué aprendizaje conviene reforzar y coordina el acompañamiento con la familia.</p></div><span className="badge"><Icono nombre="candado" />Solo el profesor del grupo</span></div>
    {estado.cargando && <Cargando mensaje="Revisando el aprendizaje del curso…" />}
    {estado.error && <ErrorAviso error={estado.error} onReintentar={estado.recargar} />}
    {datos && <>
      <p className={'mini ' + (estado.error ? 'support-history' : 'muted')}>{estado.error ? 'Se muestra la última consulta disponible. No pudimos comprobar si hay cambios.' : estado.actualizando ? 'Comprobando nuevos resultados…' : 'Consulta realizada: ' + fechaActualizacion(datos.consultado_en) + ' · Se comprueba automáticamente cada 15 segundos mientras este panel está visible.'}</p>
      <details className="support-criteria"><summary>Cómo se identifican las necesidades de apoyo</summary><p>Se revisan las últimas {datos.criterios.ventana_decisiones} decisiones evaluables por alumno y temática. Con al menos {datos.criterios.minimo_decisiones} decisiones, menos de {datos.criterios.umbral_apoyo}% seguras indica apoyo y menos de {datos.criterios.umbral_prioridad}% indica atención prioritaria.</p><p>El resumen general necesita al menos {datos.criterios.minimo_decisiones_general} decisiones en {datos.criterios.minimo_tematicas_general} temáticas y pondera por cantidad de decisiones. Una dificultad en una temática mantiene la alerta aunque el promedio general sea alto.</p><p>Son criterios iniciales de acompañamiento, ajustables por el equipo educativo. No constituyen un diagnóstico. Las muestras pequeñas no generan porcentajes; los resultados de hace más de {datos.criterios.dias_datos_antiguos} días se señalan para revisar su vigencia.</p></details>
      <div className="support-filters" role="group" aria-label="Filtrar seguimiento">
        <button type="button" aria-pressed={filtro === 'apoyo'} onClick={() => setFiltro('apoyo')}>Necesitan apoyo <b>{apoyo.length}</b></button>
        <button type="button" aria-pressed={filtro === 'pendientes'} onClick={() => setFiltro('pendientes')}>Sin evaluación suficiente <b>{pendientes.length}</b></button>
        <button type="button" aria-pressed={filtro === 'todos'} onClick={() => setFiltro('todos')}>Todos <b>{alumnos.length}</b></button>
      </div>
      {!!apoyo.length && <p className="mini muted">{apoyo.filter(a => a.estado === 'prioritario').length} con atención prioritaria. Se muestran primero las mayores necesidades de apoyo.</p>}
      {visibles.length ? <div className="support-list">{visibles.map(a => <Alumno key={a.miembro_id} alumno={a} criterios={datos.criterios} grupo={datos.nombre_grupo} />)}</div> : <div className="support-empty">
        <h3>{!alumnos.length ? 'Aún no hay alumnos vinculados' : filtro === 'apoyo' ? 'Sin alertas de apoyo en las temáticas evaluadas' : 'No hay alumnos en este filtro'}</h3>
        <p>{!alumnos.length ? 'El seguimiento aparecerá cuando las familias acepten sus invitaciones.' : pendientes.length ? `${pendientes.length} ${pendientes.length === 1 ? 'alumno aún no tiene' : 'alumnos aún no tienen'} una evaluación suficiente. La falta de datos no equivale a un buen o mal desempeño.` : 'Las necesidades de apoyo se recalculan con cada consulta. Puedes revisar las temáticas disponibles en Todos.'}</p>
      </div>}
      <p className="support-privacy"><Icono nombre="candado" />Este seguimiento incluye solo a los niños vinculados a este curso. No muestra conversaciones ni respuestas literales y no se incorpora al PDF grupal. Preparar un correo abre un borrador en tu aplicación de correo; tú decides si enviarlo.</p>
      {modoDemo && <details className="demo-controls"><summary>Probar casos de apoyo</summary><p>Estos controles reemplazan los resultados ficticios de este grupo para probar alertas y su recuperación.</p><div className="pila"><button type="button" className="boton" disabled={ocupado || !alumnos.length} onClick={() => simular(false)}>Simular casos de apoyo</button><button type="button" className="boton" disabled={ocupado || !alumnos.length} onClick={() => simular(true)}>Simular mejora</button></div>{aviso && <p aria-live="polite">{aviso}</p>}{error && <ErrorAviso error={error} />}</details>}
    </>}
  </section>
}
