import { useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { useSesion } from '@/auth/contexto'
import { PublicHeader } from '@/components/PublicHeader'
import { OttoBienvenida } from '@/components/OttoBienvenida'
import { Icono } from '@/components/Icono'
import { Marca } from '@/components/Marca'
import './bienvenida.css'

export default function BienvenidaPage() {
  const { autenticado } = useSesion()
  const [tema, setTema] = useState(0)
  const temas = [
    { nombre: 'Ciberacoso', texto: 'Reconocer cuándo algo hace daño. Y aprender a pedir ayuda.', icono: 'mensaje' as const },
    { nombre: 'Desconocidos en línea', texto: 'Aprender a poner límites y cuidar lo que compartimos.', icono: 'escudo' as const },
    { nombre: 'Retos virales', texto: 'Pensar antes de seguir un reto. Elegir también es cuidarse.', icono: 'alerta' as const },
  ]
  if (autenticado) return <Navigate to="/panel" replace />
  return <div className="bienvenida"><PublicHeader />
    <section className="welcome-hero" aria-labelledby="welcome-title">
      <div className="welcome-copy"><span className="welcome-kicker"><span />PEQUEÑAS DECISIONES. GRANDES APRENDIZAJES.</span>
        <h1 id="welcome-title">Un mundo digital<br />más seguro empieza<br />con <em>una aventura.</em></h1>
        <p>En Fishy!, niños y niñas aprenden a cuidarse en internet jugando. Las familias y comunidades educativas los acompañan, paso a paso.</p>
        <div className="welcome-actions"><Link to="/registro" className="boton public-primary">Ser parte de Fishy!<Icono nombre="flecha" /></Link><a href="#como-funciona" className="welcome-secondary">Conoce la aventura<span aria-hidden="true">↘</span></a></div>
        <div className="welcome-caption"><Icono nombre="escudo" /><span>Aprender jugando. Acompañar con información.</span></div>
      </div>
      <div className="welcome-world"><span className="world-orbit orbit-one" /><span className="world-orbit orbit-two" /><span className="world-star star-one" aria-hidden="true">✳</span><span className="world-star star-two" aria-hidden="true">✦</span><div className="world-disc"><span className="world-cloud cloud-one" /><span className="world-cloud cloud-two" /><OttoBienvenida /><div className="world-hill hill-back" /><div className="world-hill hill-front" /></div>
        <div className="world-note"><span className="world-note-icon"><Icono nombre="check" /></span><div><strong>Aprender a decidir</strong><span>Una misión a la vez.</span></div></div>
        <div className="world-tag"><span aria-hidden="true">✦</span> ¡Hola! Soy Otto.</div><span className="world-small">UNA AVENTURA CON PROPÓSITO</span>
      </div>
    </section>
    <div className="welcome-audience"><span>UN MISMO PROPÓSITO,<br /><strong>muchas formas de acompañar.</strong></span><a href="#comunidad"><Icono nombre="grupo" />Familias</a><a href="#comunidad"><Icono nombre="reportes" />Colegios</a><a href="#comunidad"><Icono nombre="escudo" />Instituciones</a></div>
    <section className="welcome-section" id="como-funciona" aria-labelledby="como-title"><div className="welcome-section-head"><div><span className="welcome-kicker">APRENDER PARA LA VIDA DIGITAL</span><h2 id="como-title">Jugar. Decidir. Conversar.</h2></div><p>El juego abre la conversación.<br />Tú acompañas lo que viene después.</p></div>
      <div className="welcome-steps">{[
        ['01', 'Una aventura que enseña', 'Situaciones del mundo digital se convierten en misiones, personajes y decisiones.', 'grupo'],
        ['02', 'Decisiones que cuentan', 'Las elecciones dentro del juego ayudan a reconocer temas que necesitan más acompañamiento.', 'check'],
        ['03', 'Información para acompañar', 'Reportes claros para conversar sobre ciberacoso, desconocidos y retos virales.', 'reportes'],
      ].map(([n, titulo, texto, icono]) => <article key={n}><div className="step-top"><span>{n}</span><Icono nombre={icono as 'grupo' | 'check' | 'reportes'} /></div><h3>{titulo}</h3><p>{texto}</p></article>)}</div>
      <div className="welcome-topics"><div><span className="welcome-kicker">CONVERSACIONES IMPORTANTES</span><h3>Lo que pasa en pantalla<br />también importa afuera.</h3><div className="topic-choices" aria-label="Explorar temáticas">{temas.map((t, i) => <button type="button" key={t.nombre} aria-pressed={i === tema} onClick={() => setTema(i)}>{t.nombre}</button>)}</div></div><div className="topic-feature" aria-live="polite"><Icono nombre={temas[tema].icono} /><h3>{temas[tema].nombre}</h3><p>{temas[tema].texto}</p></div></div>
    </section>
    <section className="welcome-community" id="comunidad" aria-labelledby="community-title"><div className="welcome-section-head"><div><span className="welcome-kicker">CRECER ES UN TRABAJO EN EQUIPO</span><h2 id="community-title">Cada persona tiene<br />un lugar en esta aventura.</h2></div><p>Una experiencia pensada para conectar el aprendizaje de niños y niñas con quienes los acompañan.</p></div><div className="community-grid">{[
      ['Familias', 'Una conversación a tiempo puede hacer la diferencia. Conoce los aprendizajes y acompaña desde casa.', 'Para madres, padres y tutores', 'grupo'],
      ['Colegios', 'Lleva la ciudadanía digital al aula. Organiza grupos y explora sus resultados de forma agregada.', 'Para docentes y comunidades educativas', 'reportes'],
      ['Instituciones', 'Conversemos sobre cómo acercar la educación y el cuidado digital a más comunidades.', 'Para organizaciones y equipos', 'escudo'],
    ].map(([titulo, texto, pie, icono]) => <article key={titulo}><Icono nombre={icono as 'grupo' | 'reportes' | 'escudo'} /><h3>{titulo}</h3><p>{texto}</p><span>{pie}</span></article>)}</div></section>
    <section className="welcome-data welcome-section" id="datos" aria-labelledby="data-title"><div><span className="welcome-kicker">DATOS CON UN PROPÓSITO CLARO</span><h2 id="data-title">Entender para acompañar.<br /><em>Eso es lo que nos mueve.</em></h2><p>Las decisiones tomadas en Fishy! se transforman en información sobre los aprendizajes del juego. Una guía para conversar, no una etiqueta para cada niño o niña.</p><a href="#contacto" className="welcome-secondary">¿Tienes preguntas sobre tus datos?<Icono nombre="flecha" /></a></div><div className="data-points"><article><span>01</span><div><h3>Qué información usamos</h3><p>Datos de la cuenta, perfiles infantiles y decisiones y progreso registrados dentro del juego.</p></div></article><article><span>02</span><div><h3>Para qué la usamos</h3><p>Para mostrar avances y resultados sobre las temáticas de seguridad digital que aborda Fishy!.</p></div></article><article><span>03</span><div><h3>Cómo se muestran los resultados</h3><p>Las familias consultan los reportes de sus perfiles. Los colegios ven resultados agregados de sus grupos.</p></div></article></div></section>
    <section className="welcome-contact" id="contacto" aria-labelledby="contact-title"><div><span className="welcome-kicker">HAGAMOS EQUIPO</span><h2 id="contact-title">Las buenas aventuras<br />empiezan con un hola.</h2><p>¿Eres parte de un colegio, una institución o una familia?<br />Nos encantará conversar contigo.</p></div><a className="contact-email" href="mailto:contacto@fishygame.cl"><Icono nombre="correo" /><span>Escríbenos<strong>contacto@fishygame.cl</strong></span><Icono nombre="flecha" /></a></section>
    <footer className="welcome-footer"><Link to="/" className="brand" aria-label="Fishy! Inicio"><Marca /></Link><span>Pequeñas decisiones para un mundo digital más seguro.</span><a href="#datos">Datos y privacidad</a><a href="mailto:contacto@fishygame.cl">Contacto</a></footer>
  </div>
}
