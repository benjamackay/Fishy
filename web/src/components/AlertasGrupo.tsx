import { useMemo, useState } from 'react'
import { TEMATICAS } from '@/types/reportes'
import type { TematicaId } from '@/types/reportes'
import type { AlertaGrupo } from '@/types/alertasGrupo'
import { alertasGrupoEjemplo } from '@/demo/alertasGrupo'
import { Icono } from './Icono'
import './alertas-grupo.css'

const niveles = {
  apoyo: { etiqueta: 'Requiere apoyo', detalle: 'Prioriza una conversación y acompaña sus próximos pasos.' },
  reforzar: { etiqueta: 'Conviene reforzar', detalle: 'Retoma el tema con una actividad breve de práctica.' },
} as const

export function AlertasGrupo({ alertas = null }: { alertas?: AlertaGrupo[] | null }) {
  const [ejemplo, setEjemplo] = useState(false)
  const [area, setArea] = useState<TematicaId | ''>('')
  const [nivel, setNivel] = useState<AlertaGrupo['nivel'] | ''>('')
  const [busqueda, setBusqueda] = useState('')
  const fuente = ejemplo ? alertasGrupoEjemplo : alertas
  const normalizar = (valor: string) => valor.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('es').trim()
  const visibles = useMemo(() => (fuente ?? []).filter(a => (!area || a.area === area) && (!nivel || a.nivel === nivel) && normalizar(a.nombre).includes(normalizar(busqueda))), [fuente, area, nivel, busqueda])
  const ninos = [...new Map(visibles.map(a => [a.miembroId, a.nombre])).entries()]
    .sort(([idA, nombreA], [idB, nombreB]) => Number(visibles.some(a => a.miembroId === idB && a.nivel === 'apoyo')) - Number(visibles.some(a => a.miembroId === idA && a.nivel === 'apoyo')) || nombreA.localeCompare(nombreB, 'es'))
  function limpiar() { setArea(''); setNivel(''); setBusqueda('') }
  function alternarEjemplo() { setEjemplo(!ejemplo); limpiar() }

  return <section className="alertas-grupo" aria-labelledby="alertas-grupo-titulo">
    <header className="alertas-cabecera">
      <div className="alertas-presentacion"><span className="alertas-simbolo"><Icono nombre="alerta" /></span><div><span className="eyebrow">ACOMPAÑAMIENTO DEL CURSO</span><h2 id="alertas-grupo-titulo">¿Quién necesita un poco más de apoyo?</h2><p>Identifica a quién acompañar y qué temática conviene trabajar.</p></div></div>
      <span className="alertas-privacidad"><Icono nombre="escudo" />Sin porcentajes individuales</span>
    </header>
    {ejemplo && <div className="alertas-ejemplo" role="status"><div><strong>Vista previa · niños y alertas ficticios</strong><p>Estos ejemplos muestran cómo se verán las alertas. No corresponden a resultados de este curso.</p></div><button className="boton" type="button" onClick={alternarEjemplo}>Cerrar ejemplo</button></div>}
    {fuente === null ? <div className="alertas-inicio"><span className="alertas-inicio-icono"><Icono nombre="mensaje" /></span><h3>Una guía para acompañar, sin comparar</h3><p>Aquí podrás reconocer qué niños necesitan apoyo en Desconocidos, Ciberacoso o Retos Virales, con orientaciones para conversar con ellos.</p><p className="mini muted">Las alertas individuales aún no están disponibles. Puedes explorar una vista de ejemplo.</p><button className="boton boton--primario" type="button" onClick={alternarEjemplo}>Ver ejemplo de alertas<Icono nombre="flecha" /></button></div> : <>
      <div className="alertas-resumen">
        <div><strong>{new Set(fuente.map(a => a.miembroId)).size}</strong><span>Niños con temas por acompañar</span></div>
        <div className="alertas-resumen-apoyo"><Icono nombre="alerta" /><span><strong>{new Set(fuente.filter(a => a.nivel === 'apoyo').map(a => a.miembroId)).size} requieren apoyo</strong><small>Para priorizar una conversación</small></span></div>
        <div><Icono nombre="mensaje" /><span><strong>{new Set(fuente.filter(a => a.nivel === 'reforzar').map(a => a.miembroId)).size} pueden reforzar</strong><small>Para seguir practicando</small></span></div>
      </div>
      <div className="alertas-filtros">
        <label className="campo">Buscar niño<input type="search" placeholder="Escribe su nombre…" value={busqueda} onChange={e => setBusqueda(e.target.value)} /></label>
        <label className="campo">Área<select value={area} onChange={e => setArea(e.target.value as TematicaId | '')}><option value="">Todas las áreas</option>{TEMATICAS.map(t => <option key={t.id} value={t.id}>{t.nombre}</option>)}</select></label>
        <label className="campo">Tipo de alerta<select value={nivel} onChange={e => setNivel(e.target.value as AlertaGrupo['nivel'] | '')}><option value="">Todas las alertas</option><option value="apoyo">Requiere apoyo</option><option value="reforzar">Conviene reforzar</option></select></label>
        {(area || nivel || busqueda) && <button className="boton" type="button" onClick={limpiar}>Limpiar filtros</button>}
      </div>
      <p className="alertas-resultados" role="status">{ninos.length} {ninos.length === 1 ? 'niño para acompañar' : 'niños para acompañar'}{area || nivel || busqueda ? ' con estos filtros' : ''}</p>
      {ninos.length ? <ul className="alertas-lista">{ninos.map(([id, nombre]) => <li key={id} className="alertas-nino"><div className="alertas-nombre"><span className="avatar small">{nombre.slice(0, 1)}</span><div><h3>{nombre}</h3><p>{visibles.filter(a => a.miembroId === id).length === 1 ? 'Una temática para trabajar juntos' : 'Temáticas para trabajar juntos'}</p></div></div><div className="alertas-areas">{visibles.filter(a => a.miembroId === id).sort((a, b) => Number(b.nivel === 'apoyo') - Number(a.nivel === 'apoyo')).map(a => {
        const tema = TEMATICAS.find(t => t.id === a.area)!
        return <details className={'alerta-area alerta-area--' + a.nivel} key={a.id}><summary><span className="alerta-tematica"><Icono nombre={tema.icono} />{tema.nombre}</span><span className="alerta-nivel">{niveles[a.nivel].etiqueta}</span><Icono nombre="desplegar" /></summary><div className="alerta-orientacion"><p>{a.observacion}</p><div><span><Icono nombre="mensaje" />Cómo acompañar</span><p>{a.sugerencia}</p></div><small>{niveles[a.nivel].detalle}</small></div></details>
      })}</div></li>)}</ul> : <div className="alertas-vacio"><Icono nombre={fuente.length ? 'usuario' : 'check'} /><h3>{fuente.length ? 'No hay coincidencias con estos filtros' : 'No hay alertas para mostrar'}</h3><p>{fuente.length ? 'Prueba con otro nombre o amplía las áreas seleccionadas.' : 'La ausencia de alertas no equivale a una evaluación completa de cada niño.'}</p>{fuente.length > 0 && <button className="boton" type="button" onClick={limpiar}>Ver todas las alertas</button>}</div>}
      <footer className="alertas-pie"><Icono nombre="escudo" /><p>Estas señales orientan el acompañamiento pedagógico. No son un diagnóstico ni una clasificación de los niños.</p></footer>
    </>}
  </section>
}
