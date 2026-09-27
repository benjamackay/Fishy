import { useEffect, useRef, useState } from 'react'
import { usePanel } from '@/hooks/usePanel'
import { Modal } from './Modal'
import { ErrorAviso } from './Aviso'
import { ApiError } from '@/lib/api'
import { comoError, ErrorUsuario } from '@/lib/errores'
import type { GrupoDetalle, PerfilFamilia } from '@/types/grupos'
import './agregar-ninos.css'

const SIN_PERFILES = 'No encontramos perfiles de niño para ese correo.'
const MAXIMO = 20
type Campos = Partial<Record<'email' | 'jugador_ids', string[]>>

/** El correo de la búsqueda queda fijado junto a sus perfiles hasta cambiar de familia. */
export function AgregarNinos({ id, modoDemo, cerrar, alGuardar }: {
  id: string; modoDemo: boolean; cerrar: () => void; alGuardar: (grupo: GrupoDetalle) => void
}) {
  const panel = usePanel()
  const [email, setEmail] = useState('')
  const [familia, setFamilia] = useState<{ email: string; perfiles: PerfilFamilia[] } | null>(null)
  const [seleccionados, setSeleccionados] = useState<number[]>([])
  const [ocupado, setOcupado] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [errorRecarga, setErrorRecarga] = useState<Error | null>(null)
  const [campos, setCampos] = useState<Campos>({})
  const vigente = useRef(true)
  const enviando = useRef(false)
  const tituloPerfiles = useRef<HTMLHeadingElement>(null)
  useEffect(() => { vigente.current = true; return () => { vigente.current = false } }, [])
  useEffect(() => { if (familia) tituloPerfiles.current?.focus() }, [familia])

  function mostrarError(e: unknown) {
    if (e instanceof ApiError && e.status === 400 && e.data && typeof e.data === 'object') {
      const datos = e.data as Record<string, unknown>
      const errores: Campos = {}
      for (const campo of ['email', 'jugador_ids'] as const) {
        const mensajes = datos[campo]
        if (Array.isArray(mensajes)) errores[campo] = mensajes.filter((m): m is string => typeof m === 'string')
        else if (typeof mensajes === 'string') errores[campo] = [mensajes]
      }
      if (Object.values(errores).some(m => m.length)) { setCampos(errores); return }
    }
    setError(comoError(e))
  }
  async function consultar(correo: string) {
    const respuesta = await panel.buscarFamilia(id, correo)
    if (!vigente.current) return
    if (!respuesta.perfiles.length) throw new ErrorUsuario(SIN_PERFILES, 404)
    setFamilia({ email: correo, perfiles: respuesta.perfiles })
  }
  async function buscar(e: React.FormEvent) {
    e.preventDefault()
    if (enviando.current) return
    enviando.current = true
    setOcupado(true); setError(null); setErrorRecarga(null); setCampos({}); setSeleccionados([]); setFamilia(null)
    try { await consultar(email.trim().toLowerCase()) }
    catch (e) { if (vigente.current) mostrarError(e) }
    finally { enviando.current = false; if (vigente.current) setOcupado(false) }
  }
  async function agregar(e: React.FormEvent) {
    e.preventDefault()
    if (!familia || enviando.current || !seleccionados.length || seleccionados.length > MAXIMO) return
    enviando.current = true
    setOcupado(true); setError(null); setErrorRecarga(null); setCampos({})
    try {
      const grupo = await panel.agregarNinos(id, familia.email, seleccionados)
      if (vigente.current) alGuardar(grupo)
    } catch (e) {
      if (!vigente.current) return
      mostrarError(e)
      if ((e instanceof ErrorUsuario || e instanceof ApiError) && e.status === 409) {
        // La selección anterior ya no es fiable: el servidor no agregó a ninguno.
        setFamilia(null); setSeleccionados([])
        try { await consultar(familia.email) }
        catch (recarga) { if (vigente.current) setErrorRecarga(comoError(recarga)) }
      }
    } finally { enviando.current = false; if (vigente.current) setOcupado(false) }
  }
  function cambiarCorreo() {
    setFamilia(null); setSeleccionados([]); setError(null); setErrorRecarga(null); setCampos({})
  }
  return <Modal titulo="Agregar niños" cerrar={cerrar} ocupado={ocupado}>
    <p className="mini muted">{familia ? 'Solo se incorporarán los perfiles que marques.' : 'Busca el correo del apoderado y selecciona solo a los niños de este curso. Se incorporarán al confirmar.'}</p>
    {modoDemo && (familia ? <span className="badge">Demostración · datos ficticios</span> : <p className="aviso">Demostración con datos ficticios. Prueba con <strong>familia.silva@example.com</strong>.</p>)}
    <form onSubmit={familia ? agregar : buscar} aria-busy={ocupado}>
      {!familia ? <label className="campo"><span>Correo del apoderado</span>
        <input autoFocus type="email" autoComplete="email" inputMode="email" required maxLength={254} value={email}
          disabled={ocupado} placeholder="familia@ejemplo.cl" aria-invalid={!!campos.email?.length} aria-describedby={campos.email?.length ? 'error-correo-familia' : undefined}
          onChange={e => { setEmail(e.target.value); setCampos({}); setError(null); setErrorRecarga(null) }} />
      </label> : <>
        <div className="familia-buscada"><div><span className="mini muted">Apoderado</span><p>{familia.email}</p></div>
          <button type="button" className="boton" onClick={cambiarCorreo} disabled={ocupado}>Cambiar correo</button></div>
        <h3 ref={tituloPerfiles} tabIndex={-1}>Elige quiénes son de este curso</h3>
        <fieldset className="familia-perfiles" disabled={ocupado} aria-describedby="limite-perfiles">
          <legend className="sr-only">Perfiles de la familia</legend>
          {familia.perfiles.map(p => <label className="familia-perfil" key={p.jugador_id}>
            <input type="checkbox" checked={p.estado === 'en_este_curso' || seleccionados.includes(p.jugador_id)}
              disabled={ocupado || p.estado !== 'disponible' || (seleccionados.length >= MAXIMO && !seleccionados.includes(p.jugador_id))}
              onChange={e => { setCampos({}); setSeleccionados(ids => e.target.checked ? [...ids, p.jugador_id] : ids.filter(id => id !== p.jugador_id)) }} />
            <span><strong>{p.nombre}</strong><small>{p.estado === 'en_este_curso' ? 'Ya está en este curso' : p.estado === 'en_otro_curso' ? 'Ya está en otro curso' : 'Disponible'}</small></span>
          </label>)}
        </fieldset>
        <p className="mini muted" id="limite-perfiles" role="status">{seleccionados.length} {seleccionados.length === 1 ? 'seleccionado' : 'seleccionados'} · Máximo {MAXIMO} por operación.</p>
        {!familia.perfiles.some(p => p.estado === 'disponible') && <p className="aviso">No hay perfiles disponibles para agregar a este curso.</p>}
      </>}
      {campos.email?.length ? <p id="error-correo-familia" className="aviso aviso--error" role="alert">{campos.email.join(' ')}</p> : null}
      {campos.jugador_ids?.length ? <p className="aviso aviso--error" role="alert">{campos.jugador_ids.join(' ')}</p> : null}
      {error && <ErrorAviso error={error} />}
      {errorRecarga && <><p className="mini muted">No pudimos actualizar los perfiles. Vuelve a buscar antes de agregar.</p><ErrorAviso error={errorRecarga} /></>}
      <div className="modal-actions"><button type="button" className="boton" disabled={ocupado} onClick={cerrar}>Cancelar</button>
        <button className="boton boton--primario" disabled={ocupado || (familia ? !seleccionados.length : !email.trim())}>
          {ocupado ? 'Procesando…' : familia ? 'Agregar' : 'Buscar'}
        </button></div>
    </form>
  </Modal>
}
