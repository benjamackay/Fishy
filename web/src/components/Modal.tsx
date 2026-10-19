import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { Icono } from './Icono'
export function Modal({ titulo, children, cerrar, ocupado = false }: { titulo: string; children: ReactNode; cerrar: () => void; ocupado?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null)
  const tituloId = useId()
  useEffect(() => {
    const dialogo = ref.current!
    dialogo.showModal()
    return () => dialogo.close()
  }, [])
  return <dialog ref={ref} className="modal" aria-labelledby={tituloId} onCancel={e => { e.preventDefault(); if (!ocupado) cerrar() }}>
    <div className="modal-heading"><h2 id={tituloId}>{titulo}</h2><button type="button" className="icon-button" disabled={ocupado} aria-label="Cerrar ventana" onClick={cerrar}><Icono nombre="cerrar" /></button></div>
    {children}
  </dialog>
}
