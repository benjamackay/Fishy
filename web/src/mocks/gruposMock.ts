/**
 * Implementacion falsa del contrato de `@/types/grupos`, mientras el backend no
 * tiene grupos. Guarda en localStorage para que crear un grupo y agregarle
 * miembros sobreviva a un F5 y el panel se pueda probar de verdad.
 *
 * El avance es deterministico a partir del `jugador_id` (no aleatorio) para que
 * los numeros no bailen entre renders.
 *
 * Todo este archivo se borra cuando Django implemente los endpoints.
 */

import { listarJugadores } from '@/api/jugadores'
import type {
  AvanceGrupo,
  AvanceMiembro,
  Grupo,
  GrupoDetalle,
  MiembroCandidato,
  MiembroGrupo,
  NuevoGrupo,
} from '@/types/grupos'

const CLAVE = 'fishy.mock.grupos'
const RETARDO_MS = 200

interface MiembroAlmacenado {
  jugador_id: number
  fecha_ingreso: string
}

interface GrupoAlmacenado {
  id: number
  nombre: string
  descripcion: string
  fecha_creacion: string
  miembros: MiembroAlmacenado[]
}

interface Almacen {
  grupos: GrupoAlmacenado[]
  siguienteId: number
}

/**
 * Perfiles de otras familias. Sin esto el panel de admin no tendria sentido: la
 * gracia es agrupar menores de cuentas distintas.
 */
const CANDIDATOS_FICTICIOS: MiembroCandidato[] = [
  { jugador_id: 9001, nombre: 'Martina', edad: 9, adulto: 'Familia Rojas' },
  { jugador_id: 9002, nombre: 'Benjamin', edad: 11, adulto: 'Familia Rojas' },
  { jugador_id: 9003, nombre: 'Sofia', edad: 8, adulto: 'Familia Contreras' },
  { jugador_id: 9004, nombre: 'Tomas', edad: 10, adulto: 'Familia Contreras' },
  { jugador_id: 9005, nombre: 'Isidora', edad: 12, adulto: 'Familia Nunez' },
  { jugador_id: 9006, nombre: 'Vicente', edad: 9, adulto: 'Familia Nunez' },
  { jugador_id: 9007, nombre: 'Antonia', edad: 10, adulto: 'Familia Silva' },
  { jugador_id: 9008, nombre: 'Matias', edad: 11, adulto: 'Familia Silva' },
]

function esperar<T>(valor: T): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(valor), RETARDO_MS))
}

function sembrar(): Almacen {
  const ahora = new Date().toISOString()
  const almacen: Almacen = {
    grupos: [
      {
        id: 1,
        nombre: '5to Basico A',
        descripcion: 'Curso piloto del taller de seguridad digital',
        fecha_creacion: ahora,
        miembros: [9001, 9003, 9005, 9007].map((jugador_id) => ({
          jugador_id,
          fecha_ingreso: ahora,
        })),
      },
    ],
    siguienteId: 2,
  }
  guardar(almacen)
  return almacen
}

function guardar(almacen: Almacen): void {
  try {
    localStorage.setItem(CLAVE, JSON.stringify(almacen))
  } catch {
    // Sin persistencia se sigue funcionando en memoria durante la sesion.
  }
}

function leer(): Almacen {
  try {
    const crudo = localStorage.getItem(CLAVE)
    if (crudo) return JSON.parse(crudo) as Almacen
  } catch {
    // localStorage inaccesible o JSON corrupto: se parte de cero.
  }
  return sembrar()
}

/** Hash deterministico: el mismo id siempre da el mismo avance. */
function semilla(id: number, sal: number): number {
  const x = Math.sin(id * 97.13 + sal * 31.7) * 10000
  return x - Math.floor(x)
}

function entre(id: number, sal: number, min: number, max: number): number {
  return Math.round(min + semilla(id, sal) * (max - min))
}

/**
 * Se cachea la PROMESA, no el resultado. La pagina del grupo dispara
 * obtenerGrupo / listarCandidatos / obtenerAvanceGrupo en paralelo, y las tres
 * pasan por aca: cacheando el resultado, las tres arrancan antes de que
 * ninguna lo haya llenado y se pide /jugadores/ una vez por llamada.
 */
let cacheCandidatos: Promise<MiembroCandidato[]> | null = null

/**
 * Perfiles reales del adulto autenticado + los ficticios de otras familias. Si
 * el backend no responde se sigue solo con los ficticios, asi el panel se puede
 * trabajar sin Django levantado.
 */
function todosLosCandidatos(): Promise<MiembroCandidato[]> {
  cacheCandidatos ??= listarJugadores()
    .then((jugadores) =>
      jugadores.map((j) => ({
        jugador_id: j.id,
        nombre: j.nombre,
        edad: j.edad,
        adulto: 'Tu cuenta',
      })),
    )
    // Sin backend: solo los ficticios.
    .catch((): MiembroCandidato[] => [])
    .then((propios) => [...propios, ...CANDIDATOS_FICTICIOS])

  return cacheCandidatos
}

function aGrupo(g: GrupoAlmacenado): Grupo {
  return {
    id: g.id,
    nombre: g.nombre,
    descripcion: g.descripcion,
    total_miembros: g.miembros.length,
    fecha_creacion: g.fecha_creacion,
  }
}

function buscar(almacen: Almacen, id: number): GrupoAlmacenado {
  const grupo = almacen.grupos.find((g) => g.id === id)
  if (!grupo) throw new Error(`No existe el grupo ${id}`)
  return grupo
}

export async function listarGrupos(): Promise<Grupo[]> {
  return esperar(leer().grupos.map(aGrupo))
}

export async function crearGrupo(datos: NuevoGrupo): Promise<Grupo> {
  const almacen = leer()
  const grupo: GrupoAlmacenado = {
    id: almacen.siguienteId,
    nombre: datos.nombre,
    descripcion: datos.descripcion ?? '',
    fecha_creacion: new Date().toISOString(),
    miembros: [],
  }
  almacen.grupos.push(grupo)
  almacen.siguienteId += 1
  guardar(almacen)
  return esperar(aGrupo(grupo))
}

export async function eliminarGrupo(id: number): Promise<void> {
  const almacen = leer()
  almacen.grupos = almacen.grupos.filter((g) => g.id !== id)
  guardar(almacen)
  return esperar(undefined)
}

export async function obtenerGrupo(id: number): Promise<GrupoDetalle> {
  const grupo = buscar(leer(), id)
  const candidatos = await todosLosCandidatos()

  const miembros: MiembroGrupo[] = grupo.miembros.map((m) => {
    const ficha = candidatos.find((c) => c.jugador_id === m.jugador_id)
    return {
      jugador_id: m.jugador_id,
      nombre: ficha?.nombre ?? `Perfil ${m.jugador_id}`,
      edad: ficha?.edad ?? null,
      adulto: ficha?.adulto ?? '-',
      fecha_ingreso: m.fecha_ingreso,
    }
  })

  return esperar({ ...aGrupo(grupo), miembros })
}

/** Perfiles que todavia no estan en el grupo. */
export async function listarCandidatos(
  grupoId: number,
): Promise<MiembroCandidato[]> {
  const grupo = buscar(leer(), grupoId)
  const dentro = new Set(grupo.miembros.map((m) => m.jugador_id))
  const candidatos = await todosLosCandidatos()
  return esperar(candidatos.filter((c) => !dentro.has(c.jugador_id)))
}

export async function agregarMiembro(
  grupoId: number,
  jugadorId: number,
): Promise<GrupoDetalle> {
  const almacen = leer()
  const grupo = buscar(almacen, grupoId)
  if (!grupo.miembros.some((m) => m.jugador_id === jugadorId)) {
    grupo.miembros.push({
      jugador_id: jugadorId,
      fecha_ingreso: new Date().toISOString(),
    })
    guardar(almacen)
  }
  return obtenerGrupo(grupoId)
}

export async function quitarMiembro(
  grupoId: number,
  jugadorId: number,
): Promise<void> {
  const almacen = leer()
  const grupo = buscar(almacen, grupoId)
  grupo.miembros = grupo.miembros.filter((m) => m.jugador_id !== jugadorId)
  guardar(almacen)
  return esperar(undefined)
}

export async function obtenerAvanceGrupo(id: number): Promise<AvanceGrupo> {
  const detalle = await obtenerGrupo(id)

  const avance_miembros: AvanceMiembro[] = detalle.miembros.map((m) => {
    const jugo = semilla(m.jugador_id, 9) > 0.15
    return {
      jugador_id: m.jugador_id,
      nombre: m.nombre,
      progreso: jugo ? entre(m.jugador_id, 2, 5, 100) : 0,
      partidas: jugo ? entre(m.jugador_id, 1, 1, 3) : 0,
      misiones_completadas: jugo ? entre(m.jugador_id, 3, 0, 12) : 0,
      zonas_completadas: jugo ? entre(m.jugador_id, 4, 0, 3) : 0,
      riesgo_total: jugo ? entre(m.jugador_id, 5, -6, 14) : 0,
      respuestas: jugo ? entre(m.jugador_id, 6, 2, 20) : 0,
      oportunidades_mejora: jugo ? entre(m.jugador_id, 7, 0, 5) : 0,
      ultima_actividad: jugo
        ? new Date(
            Date.now() - entre(m.jugador_id, 8, 0, 20) * 86400000,
          ).toISOString()
        : null,
    }
  })

  const suma = (obtener: (a: AvanceMiembro) => number) =>
    avance_miembros.reduce((total, a) => total + obtener(a), 0)

  const total = avance_miembros.length

  return esperar({
    grupo_id: id,
    progreso_promedio: total ? Math.round(suma((a) => a.progreso) / total) : 0,
    miembros_con_actividad: avance_miembros.filter((a) => a.progreso > 0).length,
    total_miembros: total,
    misiones_completadas: suma((a) => a.misiones_completadas),
    zonas_completadas: suma((a) => a.zonas_completadas),
    riesgo_total: suma((a) => a.riesgo_total),
    respuestas: suma((a) => a.respuestas),
    oportunidades_mejora: suma((a) => a.oportunidades_mejora),
    avance_miembros,
  })
}
