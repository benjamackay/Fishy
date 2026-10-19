import type { CSSProperties } from 'react'
const trazos = {
  reportes: 'M4 4h16v16H4z M8 15v-3 M12 15V8 M16 15v-5',
  grupo: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M22 21v-2a4 4 0 0 0-3-3.87 M15 3.13a4 4 0 0 1 0 7.75 M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
  escudo: 'M12 3 3 7v5c0 5 9 10 9 10s9-5 9-10V7z M8 12l3 3 5-6',
  salir: 'M9 21H4V3h5 M14 8l4 4-4 4 M8 12h12',
  flecha: 'M5 12h14 M13 6l6 6-6 6',
  volver: 'M19 12H5 M11 6l-6 6 6 6',
  mas: 'M12 5v14 M5 12h14',
  cerrar: 'm6 6 12 12 M6 18 18 6',
  descargar: 'M12 3v12 M7 10l5 5 5-5 M4 15v6h16v-6',
  reloj: 'M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0 M12 6v6l4 2',
  check: 'm5 12 4 4L19 6',
  candado: 'M5 10h14v11H5z M8 10V6a4 4 0 0 1 8 0v4',
  mensaje: 'M21 3H3v14h5v4l5-4h8z M7 8h10 M7 12h6',
  alerta: 'm12 3 10 18H2z M12 9v4 M12 17h.01',
  borrar: 'M3 6h18 M9 6V3h6v3 M5 6l1 15h12l1-15 M10 10v7 M14 10v7',
  correo: 'M3 5h18v14H3z m0 0 9 7 9-7',
  ojo: 'M2 12s3-7 10-7 10 7 10 7-3 7-10 7S2 12 2 12 M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  ojoCerrado: 'm3 3 18 18 M10.6 5.1A13 13 0 0 1 12 5c7 0 10 7 10 7a17 17 0 0 1-3 3.9 M6.3 6.3A19 19 0 0 0 2 12s3 7 10 7a13 13 0 0 0 5.7-1.3 M9.9 9.9a3 3 0 0 0 4.2 4.2',
} as const
export function Icono({ nombre, style }: { nombre: keyof typeof trazos; style?: CSSProperties }) {
  return <svg className="icon" style={style} width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={trazos[nombre]} /></svg>
}
