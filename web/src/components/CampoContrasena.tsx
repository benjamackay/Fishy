import { useState } from 'react'
import type { Ref } from 'react'
import { Icono } from './Icono'

interface Props {
  id: string
  etiqueta: string
  valor: string
  cambiar: (valor: string) => void
  ocupado: boolean
  nueva?: boolean
  error?: string
  inputRef?: Ref<HTMLInputElement>
}

export function CampoContrasena({ id, etiqueta, valor, cambiar, ocupado, nueva, error, inputRef }: Props) {
  const [visible, setVisible] = useState(false)
  return <div className="campo auth-password">
    <label htmlFor={id}>{etiqueta}</label>
    <div className="auth-password-input">
      <input ref={inputRef} id={id} type={visible ? 'text' : 'password'} value={valor}
        onChange={e => cambiar(e.target.value)} autoComplete={nueva ? 'new-password' : 'current-password'}
        required minLength={nueva ? 4 : undefined} disabled={ocupado}
        aria-invalid={error ? true : undefined} aria-describedby={error ? id + '-error' : undefined} />
      <button type="button" className="icon-button auth-password-toggle" disabled={ocupado}
        onClick={() => setVisible(v => !v)} aria-label={(visible ? 'Ocultar ' : 'Mostrar ') + (etiqueta === 'Confirmar contraseña' ? 'confirmación de contraseña' : etiqueta.toLowerCase())}
        aria-pressed={visible}><Icono nombre={visible ? 'ojoCerrado' : 'ojo'} /></button>
    </div>
    {error && <small id={id + '-error'} className="auth-field-error">{error}</small>}
  </div>
}
