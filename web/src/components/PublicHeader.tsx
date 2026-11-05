import { Link } from 'react-router-dom'
import { Marca } from './Marca'
import '../pages/bienvenida.css'

export function PublicHeader() {
  return <header className="public-header"><Link to="/" className="brand" aria-label="Fishy! Inicio"><Marca /></Link>
    <nav aria-label="Navegación del sitio"><a href="/#como-funciona">Cómo funciona</a><a href="/#comunidad">Comunidad</a><a href="/#datos">Datos y privacidad</a><a href="/#contacto">Contacto</a></nav>
    <div className="public-access"><Link to="/login" className="public-login">Iniciar sesión</Link><Link to="/registro" className="boton public-primary">Registrarse<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M5 12h14m-5-5 5 5-5 5" /></svg></Link></div>
  </header>
}
