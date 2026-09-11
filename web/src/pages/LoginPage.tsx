import { useLayoutEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { ErrorAviso } from '@/components/Aviso'
import { CampoContrasena } from '@/components/CampoContrasena'
import { FormularioRegistro } from '@/components/FormularioRegistro'
import { Icono } from '@/components/Icono'
import { Marca } from '@/components/Marca'
import { DEMO_DISPONIBLE } from '@/lib/config'
import type { InvitacionPublica } from '@/types/grupos'
import './login.css'

type Vista = 'login' | 'registro'

export default function LoginPage({ invitacion }: { invitacion?: InvitacionPublica } = {}) {
  const { entrar, entrarDemo, autenticado, esAdmin, cargando } = useSesion()
  const ubicacion = useLocation()
  const [vista, setVista] = useState<Vista>('login')
  const [nombre, setNombre] = useState('')
  const [password, setPassword] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [altura, setAltura] = useState<number>()
  const panel = useRef<HTMLDivElement>(null)
  const pestanas = useRef<(HTMLButtonElement | null)[]>([])
  const ocupado = enviando || cargando
  const desde = (ubicacion.state as { desde?: string } | null)?.desde
  const destino = desde?.startsWith('/') && !desde.startsWith('//') && desde !== '/login' ? desde : esAdmin ? '/admin/grupos' : '/'

  // Anima la altura real, también al mostrar errores o al cambiar de tamaño de pantalla.
  // Si ResizeObserver no está disponible, el panel conserva su altura natural.
  useLayoutEffect(() => {
    const elemento = panel.current
    if (!elemento || typeof ResizeObserver === 'undefined') return
    const medir = () => setAltura(elemento.getBoundingClientRect().height)
    medir()
    const observador = new ResizeObserver(medir)
    observador.observe(elemento)
    return () => observador.disconnect()
  }, [vista, autenticado])

  if (autenticado) return <Navigate to={destino} replace />

  function cambiarVista(siguiente: Vista) {
    if (ocupado || siguiente === vista) return
    setError(null)
    setPassword('')
    setVista(siguiente)
  }

  function navegarPestanas(evento: KeyboardEvent<HTMLButtonElement>) {
    if (ocupado || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(evento.key)) return
    evento.preventDefault()
    const indice = evento.key === 'Home' ? 0 : evento.key === 'End' ? 1 : vista === 'login' ? 1 : 0
    cambiarVista(indice === 0 ? 'login' : 'registro')
    pestanas.current[indice]?.focus()
  }

  async function enviar(evento: React.FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (ocupado) return
    setEnviando(true)
    setError(null)
    try { await entrar(nombre.trim(), password) }
    catch (e) { setError(e instanceof Error ? e : new Error('No pudimos iniciar sesión.')) }
    finally { setEnviando(false) }
  }

  return <div className="auth-scene">
    <div className="auth-orbits" aria-hidden="true"><span /><span /><span /></div>
    <section className="auth-card" data-view={vista} aria-label="Acceso a Fishy">
      <span className="auth-scan" key={vista} aria-hidden="true" />
      <div className="auth-topline"><div className="brand"><Marca /></div><span className="auth-caption"><Icono nombre="escudo" />Portal de tutores</span></div>
      {invitacion && <div className="aviso"><div><strong>Invitación para {invitacion.nombre_nino}</strong><p>{invitacion.grupo} · {invitacion.profesor}</p><p>Inicia sesión o regístrate con <strong>{invitacion.email}</strong>. Después confirmarás únicamente el perfil de este niño.</p></div></div>}
      <div className="auth-tabs" role="tablist" aria-label="Acceso a tu cuenta">
        <span className="auth-tab-indicator" aria-hidden="true" />
        <button ref={nodo => { pestanas.current[0] = nodo }} type="button" id="tab-login" role="tab"
          aria-selected={vista === 'login'} aria-controls="panel-login" tabIndex={vista === 'login' ? 0 : -1}
          disabled={ocupado} onClick={() => cambiarVista('login')} onKeyDown={navegarPestanas}>Iniciar sesión</button>
        <button ref={nodo => { pestanas.current[1] = nodo }} type="button" id="tab-registro" role="tab"
          aria-selected={vista === 'registro'} aria-controls="panel-registro" tabIndex={vista === 'registro' ? 0 : -1}
          disabled={ocupado} onClick={() => cambiarVista('registro')} onKeyDown={navegarPestanas}>Registrarse</button>
      </div>
      <div className="auth-panels" style={altura === undefined ? undefined : { height: altura }}>
        <div ref={panel} key={vista} id={'panel-' + vista} role="tabpanel" aria-labelledby={'tab-' + vista} className="auth-panel">
          {vista === 'login' ? <>
            <div className="auth-heading"><span className="eyebrow">TU ESPACIO DE CONFIANZA</span><h1>Qué bueno verte.</h1><p className="muted">Inicia sesión para acompañar su aprendizaje.</p></div>
            <form onSubmit={enviar} aria-label="Iniciar sesión" aria-busy={ocupado}>
              <div className="auth-fields">
                <label className="campo"><span>Nombre de usuario</span><input value={nombre} onChange={e => setNombre(e.target.value)} autoComplete="username" required maxLength={150} disabled={ocupado} /></label>
                <CampoContrasena id="login-password" etiqueta="Contraseña" valor={password} cambiar={setPassword} ocupado={ocupado} />
              </div>
              {error && <ErrorAviso error={error} />}
              <button className="boton boton--primario auth-submit full-width" disabled={ocupado || !nombre.trim()}>{enviando ? 'Iniciando sesión…' : 'Iniciar sesión'}{enviando ? <span className="spinner" aria-hidden="true" /> : <Icono nombre="flecha" />}</button>
            </form>
            {DEMO_DISPONIBLE && !invitacion && <div className="auth-demo"><span className="auth-divider">Explora con datos ficticios</span><div className="auth-demo-actions"><button type="button" className="boton" disabled={ocupado} onClick={() => entrarDemo()}>Probar como padre</button><button type="button" className="boton" disabled={ocupado} onClick={() => entrarDemo('alternativa')}>Probar como profesor</button></div><p>Padres: reportes de sus hijos. Profesores: gestión de grupos.</p></div>}
          </> : <FormularioRegistro emailInvitacion={invitacion?.email} ocupado={ocupado} cambiarOcupado={setEnviando} volverAlLogin={usuario => { setNombre(usuario); cambiarVista('login') }} />}
        </div>
        <div id={vista === 'login' ? 'panel-registro' : 'panel-login'} role="tabpanel" aria-labelledby={vista === 'login' ? 'tab-registro' : 'tab-login'} hidden />
      </div>
      <p className="auth-privacy"><Icono nombre="candado" /><span>Solo tendrás acceso a los reportes permitidos para tu cuenta.</span></p>
    </section>
  </div>
}
