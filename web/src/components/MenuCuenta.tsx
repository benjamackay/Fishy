import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { Icono } from './Icono'

export function MenuCuenta() {
  const { perfil, salir, esProfesor } = useSesion()
  const [abierto, setAbierto] = useState(false)
  const contenedor = useRef<HTMLDivElement>(null)
  const boton = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (!abierto) return
    const cerrarFuera = (e: PointerEvent) => {
      if (!contenedor.current?.contains(e.target as Node)) setAbierto(false)
    }
    const cerrarEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setAbierto(false); boton.current?.focus() }
    }
    document.addEventListener('pointerdown', cerrarFuera)
    document.addEventListener('keydown', cerrarEscape)
    return () => {
      document.removeEventListener('pointerdown', cerrarFuera)
      document.removeEventListener('keydown', cerrarEscape)
    }
  }, [abierto])
  const rol = esProfesor ? 'Tutor administrador' : perfil?.rol === 'padre' ? 'Tutor padre/madre' : perfil?.rol === 'admin' ? 'Administrador de Fishy' : 'Cuenta sin rol asignado'
  return <div className="account cuenta-menu" ref={contenedor} onBlur={e => {
    if (!e.currentTarget.contains(e.relatedTarget)) setAbierto(false)
  }}>
    <button ref={boton} type="button" className="cuenta-trigger" aria-label="Menú de usuario" aria-expanded={abierto} aria-controls="menu-cuenta" onClick={() => setAbierto(v => !v)}>
      <span className="avatar small">{perfil?.nombre.slice(0, 1).toUpperCase()}</span>
      <span className="cuenta-identidad"><strong>{perfil?.nombre}</strong><span>{rol}</span></span>
      <Icono nombre="desplegar" />
    </button>
    {abierto && <div className="cuenta-desplegable" id="menu-cuenta">
      <div className="cuenta-resumen"><strong>Mi cuenta</strong><span>{perfil?.email}</span></div>
      <Link to="/configuracion" onClick={() => setAbierto(false)}><Icono nombre="configuracion" />Configuración</Link>
      <button type="button" onClick={() => { setAbierto(false); salir() }}><Icono nombre="salir" />Cerrar sesión</button>
    </div>}
  </div>
}
