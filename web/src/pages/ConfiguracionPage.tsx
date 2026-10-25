import { useState } from 'react'
import type { FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { Icono } from '@/components/Icono'
import { CampoContrasena } from '@/components/CampoContrasena'
import './configuracion.css'

export default function ConfiguracionPage() {
  const { perfil, salir } = useSesion()
  const [params, setParams] = useSearchParams()
  const seguridad = params.get('seccion') === 'seguridad'
  const inicial = { nombre: perfil?.nombre ?? '', apellido: perfil?.apellido ?? '', email: perfil?.email ?? '', fecha: perfil?.fecha_nacimiento ?? '' }
  const [datos, setDatos] = useState(inicial)
  const [claves, setClaves] = useState({ actual: '', nueva: '', confirmar: '' })
  const [mensaje, setMensaje] = useState('')
  const [error, setError] = useState('')
  const modificado = Object.keys(datos).some(k => datos[k as keyof typeof datos] !== inicial[k as keyof typeof datos])
  function cambiarSeccion(seccion: string) {
    setMensaje(''); setError(''); setClaves({ actual: '', nueva: '', confirmar: '' })
    setParams(seccion === 'seguridad' ? { seccion } : {})
  }
  function revisarPerfil(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setError(''); setMensaje('')
    if (!datos.nombre.trim()) { setError('Ingresa un nombre de usuario.'); return }
    if (datos.fecha && new Date(datos.fecha + 'T00:00:00') > new Date()) { setError('La fecha de nacimiento no puede ser futura.'); return }
    setMensaje('Los datos son válidos. Esta vista previa todavía no aplica cambios a tu cuenta.')
  }
  function revisarClave(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setError(''); setMensaje('')
    if (claves.nueva.length < 8) { setError('La nueva contraseña debe tener al menos 8 caracteres.'); return }
    if (claves.nueva === claves.actual) { setError('Elige una contraseña diferente a la actual.'); return }
    if (claves.nueva !== claves.confirmar) { setError('Las contraseñas no coinciden.'); return }
    setClaves({ actual: '', nueva: '', confirmar: '' })
    setMensaje('El formato es válido. Tu contraseña actual no se ha verificado ni modificado en esta vista previa.')
  }
  return <div className="configuracion">
    <div className="config-cabecera"><span className="eyebrow">MI CUENTA</span><h1>Configuración</h1><p className="muted">Un espacio para tus datos y la seguridad de tu cuenta.</p></div>
    <div className="config-layout">
      <aside className="config-sidebar">
        <div className="config-persona"><span className="avatar">{perfil?.nombre.slice(0, 1).toUpperCase()}</span><div><strong>{perfil?.nombre}</strong><span>Cuenta personal</span></div></div>
        <nav aria-label="Configuración de la cuenta">
          <button type="button" className={'config-nav ' + (!seguridad ? 'seleccionado' : '')} aria-current={!seguridad ? 'page' : undefined} onClick={() => cambiarSeccion('perfil')}><Icono nombre="usuario" />Perfil</button>
          <button type="button" className={'config-nav ' + (seguridad ? 'seleccionado' : '')} aria-current={seguridad ? 'page' : undefined} onClick={() => cambiarSeccion('seguridad')}><Icono nombre="candado" />Contraseña y seguridad</button>
        </nav>
        <p className="config-ayuda"><Icono nombre="escudo" />Mantén tus datos actualizados y elige una contraseña segura.</p>
      </aside>
      <div className="config-panel">
        <div className="config-preview"><span className="badge badge--warm">Vista previa</span><p>Puedes completar y revisar los formularios. Los cambios estarán disponibles cuando se habilite su guardado en la cuenta.</p></div>
        <section aria-labelledby="config-titulo">
          <div className="config-titulo"><h2 id="config-titulo">{seguridad ? 'Contraseña y seguridad' : 'Información personal'}</h2><p>{seguridad ? 'Mantén tu cuenta protegida con una contraseña segura.' : 'Administra la información asociada a tu cuenta de Fishy!.'}</p></div>
          {!seguridad ? <form aria-label="Información personal" onSubmit={revisarPerfil}>
            <div className="config-avatar"><span className="avatar square">{perfil?.nombre.slice(0, 1).toUpperCase()}</span><div><strong>Tu perfil en Fishy!</strong><p>Tu inicial identifica tu cuenta.</p></div></div>
            <label className="campo">Nombre de usuario<input autoComplete="username" required maxLength={150} value={datos.nombre} onChange={e => { setMensaje(''); setDatos({ ...datos, nombre: e.target.value }) }} /><small>Es el nombre que usas para iniciar sesión.</small></label>
            <label className="campo">Apellido<input autoComplete="family-name" maxLength={150} value={datos.apellido} onChange={e => { setMensaje(''); setDatos({ ...datos, apellido: e.target.value }) }} /></label>
            <label className="campo">Correo electrónico<input type="email" autoComplete="email" required maxLength={254} value={datos.email} onChange={e => { setMensaje(''); setDatos({ ...datos, email: e.target.value }) }} /><small>Usa un correo al que tengas acceso.</small></label>
            <label className="campo">Fecha de nacimiento <span className="sr-only">(opcional)</span><input type="date" autoComplete="bday" value={datos.fecha} onChange={e => { setMensaje(''); setDatos({ ...datos, fecha: e.target.value }) }} /><small>Opcional.</small></label>
            {error && <p className="aviso aviso--error" role="alert">{error}</p>}
            {mensaje && <p className="aviso" role="status">{mensaje}</p>}
            <div className="config-acciones"><button className="boton boton--primario" disabled={!modificado}>Revisar cambios</button><button type="button" className="boton" disabled={!modificado} onClick={() => { setDatos(inicial); setError(''); setMensaje('') }}>Descartar cambios</button></div>
          </form> : <>
            <form className="config-password" aria-label="Cambiar contraseña" onSubmit={revisarClave}>
              <CampoContrasena id="clave-actual" etiqueta="Contraseña actual" valor={claves.actual} cambiar={actual => { setMensaje(''); setError(''); setClaves({ ...claves, actual }) }} ocupado={false} />
              <CampoContrasena id="clave-nueva" etiqueta="Nueva contraseña" valor={claves.nueva} cambiar={nueva => { setMensaje(''); setError(''); setClaves({ ...claves, nueva }) }} ocupado={false} nueva />
              <p className="config-clave-ayuda">Usa al menos 8 caracteres. Combina palabras, números y símbolos para crear una contraseña difícil de adivinar.</p>
              <CampoContrasena id="clave-confirmar" etiqueta="Confirmar contraseña" valor={claves.confirmar} cambiar={confirmar => { setMensaje(''); setError(''); setClaves({ ...claves, confirmar }) }} ocupado={false} nueva />
              {error && <p className="aviso aviso--error" role="alert">{error}</p>}
              {mensaje && <p className="aviso" role="status">{mensaje}</p>}
              <div className="config-acciones"><button className="boton boton--primario">Revisar contraseña</button></div>
            </form>
            <div className="config-sesion"><div><h3>Sesión actual</h3><p>Sal de tu cuenta en este navegador.</p></div><button type="button" className="boton" onClick={salir}><Icono nombre="salir" />Cerrar sesión</button></div>
          </>}
        </section>
      </div>
    </div>
  </div>
}
