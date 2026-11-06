import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Icono } from './Icono'

export function CodigoRecuperacion({ email, cambiarCorreo, verificarCodigo }: { email: string; cambiarCorreo: () => void; verificarCodigo: (codigo: string) => Promise<void> }) {
  const [digitos, setDigitos] = useState<string[]>(Array(6).fill(''))
  const [error, setError] = useState(false)
  const [ocupado, setOcupado] = useState(false)
  const [fallo, setFallo] = useState('')
  const casillas = useRef<(HTMLInputElement | null)[]>([])
  useEffect(() => { casillas.current[0]?.focus() }, [])

  function completar(valor: string, indice: number) {
    if (!/^\d*$/.test(valor)) return
    setError(false); setFallo('')
    const siguientes = [...digitos]
    const numeros = valor.slice(0, 6 - indice).split('')
    if (!numeros.length) siguientes[indice] = ''
    else numeros.forEach((numero, i) => { siguientes[indice + i] = numero })
    setDigitos(siguientes)
    if (numeros.length) casillas.current[Math.min(indice + numeros.length, 5)]?.focus()
  }
  async function verificar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const vacio = digitos.findIndex(d => !/^\d$/.test(d))
    if (vacio !== -1) { setError(true); casillas.current[vacio]?.focus(); return }
    setError(false); setFallo(''); setOcupado(true)
    try { await verificarCodigo(digitos.join('')) }
    catch (e) { setFallo(e instanceof Error ? e.message : 'No se pudo verificar el código.') }
    finally { setOcupado(false) }
  }
  return <>
    <div className="auth-heading"><span className="welcome-kicker">UN PASO MÁS PARA VOLVER</span><h1>Ingresa tu código</h1><p>Escribe el código numérico de 6 dígitos asociado a este correo:</p></div>
    <p className="recovery-email">{email}</p>
    <form aria-label="Ingresar código de recuperación" onSubmit={verificar} noValidate>
      <fieldset className="recovery-code" disabled={ocupado}><legend>Código de recuperación</legend><div className="recovery-code-boxes">
        {digitos.map((digito, i) => <input key={i} ref={nodo => { casillas.current[i] = nodo }} type="text" inputMode="numeric" autoComplete={i === 0 ? 'one-time-code' : 'off'} maxLength={6} pattern="[0-9]" required value={digito}
          aria-label={'Dígito ' + (i + 1) + ' de 6'} aria-invalid={error || undefined} aria-describedby={error ? 'codigo-error' : 'codigo-ayuda'}
          onFocus={e => e.currentTarget.select()} onChange={e => completar(e.target.value, i)}
          onPaste={e => { e.preventDefault(); const texto = e.clipboardData.getData('text').trim(); if (/^\d[\d\s-]*$/.test(texto)) completar(texto.replace(/[\s-]/g, ''), i) }}
          onKeyDown={e => {
            if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { e.preventDefault(); casillas.current[Math.max(0, Math.min(5, i + (e.key === 'ArrowLeft' ? -1 : 1)))]?.focus() }
            if (e.key === 'Backspace' && !digitos[i] && i > 0) { e.preventDefault(); completar('', i - 1); casillas.current[i - 1]?.focus() }
          }} />)}
      </div></fieldset>
      {error ? <p className="recovery-code-error" role="alert" id="codigo-error">Completa los 6 dígitos del código.</p> : <p className="recovery-note" id="codigo-ayuda">Puedes escribir los números o pegar el código completo.</p>}
      <button disabled={ocupado} className="boton boton--primario auth-submit full-width">Verificar código<Icono nombre="flecha" /></button>
    </form>
    {fallo && <p role="alert" className="aviso aviso--error">{fallo}</p>}
    <button type="button" className="recovery-change-email" disabled={ocupado} onClick={cambiarCorreo}>Usar otro correo</button>
  </>
}
