import { ErrorUsuario } from '@/lib/errores'
import { hayResultados, porcentajeSeguro, tematicasCompletas } from '@/lib/reportes'
import { EVENTO_DATOS } from '@/hooks/useDatosVivos'
import type { FuentePanel } from '@/types/panel'
import type { Grupo, GrupoDetalle, NuevoGrupo } from '@/types/grupos'
import type { NinoResumen, ReporteNino, ReporteGrupo, ResultadoTematica, TematicaId } from '@/types/reportes'
import { TEMATICAS } from '@/types/reportes'
import { perfilesDemo } from './sesionDemo'

/** Datos ficticios, separados de auth real y por cuenta. Jamás son un fallback de red. */
export const MINIMO_PARTICIPANTES_DEMO = 3
interface RegistroDemo { id: string; email: string; nino_id: number }
interface GrupoGuardado extends GrupoDetalle { actualizado_en: string }
interface Almacen {
  version: 2
  ninos: ReporteNino[]
  usuarios: RegistroDemo[]
  grupos: GrupoGuardado[]
}
const memoria = new Map<number, Almacen>()
const clave = (adultoId: number) => 'fishy.demo.panel.v2.' + adultoId
const copia = <T,>(valor: T): T => structuredClone(valor)
function resultado(tematica: TematicaId, seguras: number | null, evaluadas = 10, completada = true): ResultadoTematica {
  return { tematica, metricas: seguras === null ? null : { decisiones_seguras: seguras, decisiones_evaluadas: evaluadas, completada } }
}
function sembrar(adultoId: number): Almacen {
  const ahora = new Date().toISOString()
  const principal = adultoId === 1001
  const ids = principal ? [101, 102, 901, 902, 903] : [103, 904, 905, 906]
  const nombres = principal ? ['Martina', 'Tomás', 'Florencia', 'Mateo', 'Antonia'] : ['Sofía', 'Lucas', 'Emilia', 'Agustín']
  const correos = principal
    ? ['familia.rojas@example.com', 'familia.tomas@example.com', 'familia.flores@example.com', 'familia.perez@example.com', 'familia.diaz@example.com']
    : ['familia.silva@example.com', 'familia.lucas@example.com', 'familia.emilia@example.com', 'familia.agustin@example.com']
  const ninos = ids.map((id, i): ReporteNino => {
    const vacio = principal && i === 1
    const tematicas = [
      resultado('desconocidos', vacio ? null : 8, 10),
      resultado('ciberacoso', vacio ? null : 6, 8, false),
      resultado('retos_virales', vacio || i === 0 ? null : 4, 5),
    ]
    const nino: NinoResumen = { id, adulto_id: principal && i < 2 ? adultoId : -1, nombre: nombres[i], edad: 9 + i % 3, actualizado_en: vacio ? null : ahora, tematicas }
    return { nino, actualizado_en: nino.actualizado_en, tematicas }
  })
  const usuarios = ids.map((nino_id, i) => ({ id: 'usuario-' + nino_id, email: correos[i], nino_id }))
  const miembros = usuarios.filter((_, i) => !(principal && i === 1)).map(u => ({ id: u.id, email: u.email, fecha_ingreso: ahora }))
  return { version: 2, ninos, usuarios, grupos: [
    { id: 'grupo-demo-' + adultoId + '-a', nombre: '5° Básico A', descripcion: 'Taller de ciudadanía y seguridad digital.', total_miembros: miembros.length, fecha_creacion: ahora, actualizado_en: ahora, miembros },
    { id: 'grupo-demo-' + adultoId + '-b', nombre: 'Taller de bienvenida', descripcion: 'Un nuevo espacio para aprender juntos.', total_miembros: 0, fecha_creacion: ahora, actualizado_en: ahora, miembros: [] },
  ] }
}
function leer(adultoId: number): Almacen {
  let raw: string | null = null
  try { raw = localStorage.getItem(clave(adultoId)) } catch { return copia(migrarVinculosProfesor(adultoId, memoria.get(adultoId) ?? iniciar(adultoId))) }
  if (raw) {
    let datos: Almacen
    try { datos = JSON.parse(raw) as Almacen } catch { throw new ErrorUsuario('No pudimos leer los datos guardados de la demostración.') }
    if (datos.version !== 2 || !Array.isArray(datos.grupos) || !Array.isArray(datos.ninos) || !Array.isArray(datos.usuarios)) throw new ErrorUsuario('Los datos guardados de la demostración no son compatibles.')
    memoria.set(adultoId, datos)
    return copia(migrarVinculosProfesor(adultoId, datos))
  }
  return iniciar(adultoId)
}

/** Corrige la demo anterior sin perder grupos, integrantes ni progreso guardado. */
function migrarVinculosProfesor(adultoId: number, datos: Almacen): Almacen {
  if (adultoId !== perfilesDemo.alternativa.id) return datos
  const vinculados = datos.ninos.filter(r => r.nino.adulto_id === adultoId)
  if (vinculados.length) {
    for (const reporte of vinculados) reporte.nino.adulto_id = -1
    guardar(adultoId, datos, false)
  }
  return datos
}
function iniciar(adultoId: number): Almacen {
  const datos = sembrar(adultoId)
  guardar(adultoId, datos, false)
  return copia(datos)
}
function guardar(adultoId: number, datos: Almacen, avisar = true) {
  memoria.set(adultoId, copia(datos))
  try { localStorage.setItem(clave(adultoId), JSON.stringify(datos)) } catch { /* La demo continúa en memoria cuando el navegador impide guardar. */ }
  if (avisar) window.dispatchEvent(new Event(EVENTO_DATOS))
}
function buscarGrupo(datos: Almacen, id: string): GrupoGuardado {
  const grupo = datos.grupos.find(g => g.id === id)
  if (!grupo) throw new ErrorUsuario('No encontramos este grupo en tu cuenta.')
  return grupo
}
function resumen(g: GrupoGuardado): Grupo {
  return { id: g.id, nombre: g.nombre, descripcion: g.descripcion, total_miembros: g.miembros.length, fecha_creacion: g.fecha_creacion }
}
function detalle(g: GrupoGuardado): GrupoDetalle { return { ...resumen(g), miembros: copia(g.miembros) } }
function mutar<T>(adultoId: number, accion: (datos: Almacen) => T): Promise<T> {
  const ejecutar = () => {
    const datos = leer(adultoId)
    const retorno = accion(datos)
    guardar(adultoId, datos)
    return retorno
  }
  return navigator.locks ? navigator.locks.request(clave(adultoId), ejecutar) : Promise.resolve().then(ejecutar)
}
export function normalizarCorreo(email: string): string {
  return email.trim().toLowerCase()
}
export function validarGrupo(datos: NuevoGrupo): NuevoGrupo {
  const nombre = datos.nombre.trim()
  const descripcion = datos.descripcion?.trim() ?? ''
  if (!nombre || nombre.length > 80) throw new ErrorUsuario('Ingresa un nombre de entre 1 y 80 caracteres.')
  if (descripcion.length > 280) throw new ErrorUsuario('La descripción puede tener hasta 280 caracteres.')
  return { nombre, descripcion }
}
export function crearPanelDemo(adultoId: number): FuentePanel {
  return {
    listarNinos: async () => copia(leer(adultoId).ninos.filter(r => r.nino.adulto_id === adultoId).map(r => ({ ...r.nino, actualizado_en: r.actualizado_en, tematicas: r.tematicas }))),
    obtenerReporteNino: async id => {
      const reporte = leer(adultoId).ninos.find(r => r.nino.id === id && r.nino.adulto_id === adultoId)
      if (!reporte) throw new ErrorUsuario('No tienes acceso a este reporte.')
      return copia(reporte)
    },
    listarGrupos: async () => leer(adultoId).grupos.map(resumen),
    crearGrupo: datos => mutar(adultoId, almacen => {
      const valores = validarGrupo(datos)
      const ahora = new Date().toISOString()
      const grupo: GrupoGuardado = { id: crypto.randomUUID(), nombre: valores.nombre, descripcion: valores.descripcion ?? '', total_miembros: 0, miembros: [], fecha_creacion: ahora, actualizado_en: ahora }
      almacen.grupos.unshift(grupo)
      return resumen(grupo)
    }),
    obtenerGrupo: async id => detalle(buscarGrupo(leer(adultoId), id)),
    agregarUsuario: (id, correo) => mutar(adultoId, almacen => {
      const grupo = buscarGrupo(almacen, id)
      const email = normalizarCorreo(correo)
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 254) throw new ErrorUsuario('Ingresa un correo electrónico válido.')
      if (grupo.miembros.some(m => normalizarCorreo(m.email) === email)) throw new ErrorUsuario('El usuario ya forma parte del grupo.')
      const usuario = almacen.usuarios.find(u => u.email === email)
      if (!usuario) throw new ErrorUsuario('No encontramos un usuario registrado con ese correo.')
      grupo.miembros.push({ id: usuario.id, email, fecha_ingreso: new Date().toISOString() })
      grupo.actualizado_en = new Date().toISOString()
      return detalle(grupo)
    }),
    eliminarUsuario: (id, miembroId) => mutar(adultoId, almacen => {
      const grupo = buscarGrupo(almacen, id)
      if (!grupo.miembros.some(m => m.id === miembroId)) throw new ErrorUsuario('Este usuario ya no forma parte del grupo.')
      grupo.miembros = grupo.miembros.filter(m => m.id !== miembroId)
      grupo.actualizado_en = new Date().toISOString()
    }),
    eliminarGrupo: id => mutar(adultoId, almacen => {
      buscarGrupo(almacen, id)
      almacen.grupos = almacen.grupos.filter(g => g.id !== id)
    }),
    obtenerReporteGrupo: async id => {
      const almacen = leer(adultoId)
      const grupo = buscarGrupo(almacen, id)
      const ids = new Set(grupo.miembros.map(m => almacen.usuarios.find(u => u.id === m.id)?.nino_id))
      const reportes = almacen.ninos.filter(r => ids.has(r.nino.id))
      const tematicas = TEMATICAS.map((t): ResultadoTematica => {
        const metricas = reportes.map(r => r.tematicas.find(x => x.tematica === t.id)?.metricas).filter(m => porcentajeSeguro(m) !== null)
        if (metricas.length < MINIMO_PARTICIPANTES_DEMO) return { tematica: t.id, metricas: null, motivo: metricas.length ? 'muestra_insuficiente' : 'sin_resultados' }
        return { tematica: t.id, metricas: {
          decisiones_seguras: metricas.reduce((sum, m) => sum + m!.decisiones_seguras, 0),
          decisiones_evaluadas: metricas.reduce((sum, m) => sum + m!.decisiones_evaluadas, 0),
          completada: metricas.every(m => m!.completada),
        } }
      })
      const fechas = [grupo.actualizado_en, ...reportes.map(r => r.actualizado_en).filter((f): f is string => !!f)]
      const reporte: ReporteGrupo = { grupo_id: id, nombre_grupo: grupo.nombre, total_integrantes: grupo.miembros.length,
        participantes_con_resultados: reportes.filter(r => hayResultados(r.tematicas)).length,
        minimo_participantes: MINIMO_PARTICIPANTES_DEMO, actualizado_en: fechas.sort().at(-1) ?? null, tematicas }
      return reporte
    },
  }
}
/** Simula un registro del juego. Solo se invoca desde controles rotulados como demostración. */
export function simularProgreso(adultoId: number, ninoId: number, tematica: TematicaId): Promise<void> {
  return mutar(adultoId, almacen => {
    const r = almacen.ninos.find(r => r.nino.id === ninoId && r.nino.adulto_id === adultoId)
    if (!r) throw new ErrorUsuario('No tienes acceso a este reporte.')
    r.tematicas = tematicasCompletas(r.tematicas)
    const tema = r.tematicas.find(t => t.tematica === tematica)!
    tema.metricas = { decisiones_seguras: (tema.metricas?.decisiones_seguras ?? 0) + 4, decisiones_evaluadas: (tema.metricas?.decisiones_evaluadas ?? 0) + 5, completada: true }
    r.actualizado_en = new Date().toISOString()
  })
}
export function correosDemo(adultoId: number): string[] { return leer(adultoId).usuarios.map(u => u.email) }
