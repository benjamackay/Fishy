import type { AlertaGrupo } from '@/types/alertasGrupo'

/** Ejemplos independientes de cualquier resultado real del juego. */
export const alertasGrupoEjemplo: AlertaGrupo[] = [
  { id: 'ejemplo-sofia-desconocidos', miembroId: 'ejemplo-sofia', nombre: 'Sofía', area: 'desconocidos', nivel: 'apoyo', observacion: 'Necesita acompañamiento para reconocer cuándo una persona desconocida solicita información personal.', sugerencia: 'Conversen sobre qué datos son privados y practiquen cómo pedir ayuda a una persona adulta de confianza.' },
  { id: 'ejemplo-lucas-ciberacoso', miembroId: 'ejemplo-lucas', nombre: 'Lucas', area: 'ciberacoso', nivel: 'apoyo', observacion: 'Necesita apoyo para identificar cómo actuar cuando alguien recibe mensajes que le hacen daño.', sugerencia: 'Trabajen un caso breve: acompañar a quien recibe el daño, guardar evidencia y buscar ayuda sin compartir los mensajes.' },
  { id: 'ejemplo-sofia-retos', miembroId: 'ejemplo-sofia', nombre: 'Sofía', area: 'retos_virales', nivel: 'reforzar', observacion: 'Conviene reforzar cómo detenerse antes de aceptar un reto por presión del grupo.', sugerencia: 'Practiquen maneras de decir que no y de proponer una alternativa segura para participar con sus compañeros.' },
  { id: 'ejemplo-emilia-desconocidos', miembroId: 'ejemplo-emilia', nombre: 'Emilia', area: 'desconocidos', nivel: 'reforzar', observacion: 'Conviene reforzar la diferencia entre un contacto conocido y alguien que solo conoce por internet.', sugerencia: 'Revisen juntos señales de confianza y acuerden qué hacer cuando una conversación genera dudas.' },
]
