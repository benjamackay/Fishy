using System;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Chat
{
    /// <summary>
    /// Registro de una sesión de chat en el backend (HDU-8 / celular diegético).
    ///
    /// <b>Graba la conversación entera y la manda de una vez al terminar.</b> Antes cada
    /// línea era su propio POST en el momento de decirse; ahora se anotan en memoria y
    /// <see cref="LogEnd"/> encola la conversación completa en <see cref="ColaDeCambios"/>,
    /// que la sube cuando el juego cambia de zona o se cierra.
    ///
    /// <b>Por qué se encola solo al terminar, y no mensaje a mensaje.</b> Las peticiones
    /// del chat son una cadena: el id de la conversación no existe hasta que contesta
    /// <c>IniciarChat</c>, y cada mensaje cuelga de ese id. Si se encolaran sueltas, un
    /// vaciado a mitad de conversación —que puede pasar: Otto camina durante los chats
    /// del celular y puede cruzar de zona— mandaría media cadena y el cierre nunca
    /// llegaría. Encolando solo al final, una conversación sin terminar sencillamente no
    /// está en la cola. Es correcto por construcción.
    ///
    /// Esto además <b>quita</b> complejidad: la cola de acciones pendientes que había
    /// aquí existía justo porque el <c>ChatId</c> tardaba en llegar. Mandando al final,
    /// ya está resuelto antes del primer mensaje.
    ///
    /// <b>Lo que se pierde:</b> si el juego muere a media conversación, no queda nada.
    /// Antes el backend conservaba los mensajes ya enviados con <c>fecha_termino</c> en
    /// NULL — a medias, pero visible en el reporte del adulto. Es consecuencia directa
    /// de que la cola no se persista a disco, y es una decisión tomada: o la conversación
    /// entera o nada.
    ///
    /// El campo <c>preguntaBancoId</c> vincula cada mensaje con la pregunta del banco
    /// que lo originó (ej. "HDU2_NPC01_F2_Q01"), permitiendo análisis pedagógico.
    /// </summary>
    public class ChatBackendLogger
    {
        /// <summary>Una línea de la conversación, tal como la espera el endpoint.</summary>
        private class MensajeGrabado
        {
            public string Tipo;               // start | request | chain
            public string Texto;
            public string Calidad;
            public List<OpcionRespuesta> Opciones;
            public string PreguntaBancoId;
            public string OpcionBancoId;
        }

        /// <summary>Para que cada conversación tenga su propia clave en la cola.</summary>
        private static int _secuencia;

        private readonly List<MensajeGrabado> _mensajes = new List<MensajeGrabado>();

        private string _contacto;
        private string _zona;
        private string _categoriaRiesgo;
        private bool _failed;
        private bool _ended;

        /// <summary>True si la conversación se está grabando para subirla.</summary>
        public bool Enabled { get; private set; }

        // ── Inicio de sesión ───────────────────────────────────────────────────

        /// <summary>
        /// Empieza a grabar. Ya no llama al backend: solo anota con quién se habla y
        /// comprueba que haya sesión y partida, porque sin eso no habrá dónde guardar.
        /// Devuelve false en ese caso, igual que antes, y el chat sigue funcionando.
        /// </summary>
        public bool Begin(string contactName, string zoneId, string categoriaRiesgo)
        {
            var api = ApiManager.Instance;
            if (api == null || !api.IsLoggedIn || api.PartidaId == null)
            {
                _failed = true;
                return false;
            }

            _contacto = contactName;
            _zona = zoneId;
            _categoriaRiesgo = categoriaRiesgo;
            Enabled = true;
            return true;
        }

        // ── Log de mensajes ────────────────────────────────────────────────────

        /// <summary>Mensaje inicial del NPC (tipo "start").</summary>
        public void LogStart(string npcText, string preguntaBancoId = null)
            => Anotar(new MensajeGrabado
            {
                Tipo = "start",
                Texto = npcText,
                PreguntaBancoId = preguntaBancoId,
            });

        /// <summary>Mensaje del NPC con opciones de respuesta (tipo "request").</summary>
        public void LogRequest(string npcText, List<OpcionRespuesta> opciones, string preguntaBancoId = null)
            => Anotar(new MensajeGrabado
            {
                Tipo = "request",
                Texto = npcText,
                Calidad = "",
                // Copia: la lista de opciones la reutiliza el módulo de chat para el
                // siguiente nodo, y para cuando esto se suba ya sería otra cosa.
                Opciones = opciones != null ? new List<OpcionRespuesta>(opciones) : null,
                PreguntaBancoId = preguntaBancoId,
            });

        /// <summary>
        /// Respuesta elegida por el jugador (tipo "chain").
        ///
        /// <paramref name="opcionBancoId"/> es lo que hace que esta respuesta cuente
        /// para el riesgo por zona: identifica la opción exacta del banco, con su
        /// puntaje real (-1 / +1 / +2). Sin él la respuesta se registra igual, pero
        /// no suma.
        /// </summary>
        public void LogChoice(string playerText, string calidad, string preguntaBancoId = null,
            string opcionBancoId = null)
            => Anotar(new MensajeGrabado
            {
                Tipo = "chain",
                Texto = playerText,
                Calidad = calidad,
                PreguntaBancoId = preguntaBancoId,
                OpcionBancoId = opcionBancoId,
            });

        /// <summary>
        /// Cierra la conversación y la pone en la cola.
        ///
        /// Idempotente: solo la primera llamada cuenta. Hace falta porque el cierre se
        /// puede disparar por más de un camino (nodo con <c>closesChat</c>, fin de
        /// conversación, o el jugador cerrando el chat), y encolar dos veces la misma
        /// conversación la duplicaría en la base.
        /// </summary>
        public void LogEnd(string mensajeCierre = "")
        {
            if (_ended || !Enabled || _failed) return;
            _ended = true;

            // Una conversación sin una sola línea no vale la pena: crearía un NPC y un
            // chat vacíos en la base que el reporte del adulto tendría que filtrar.
            if (_mensajes.Count == 0) return;

            // Se copia todo a locales: este logger se suelta en cuanto termina el chat
            // (ChatModuleController lo pone a null) y la cola puede tardar en vaciarse.
            string contacto = _contacto;
            string zona = _zona;
            string categoria = _categoriaRiesgo;
            string cierre = mensajeCierre;
            var mensajes = new List<MensajeGrabado>(_mensajes);

            ColaDeCambios.EncolarCadena($"chat:{_secuencia++}",
                (ok, error) => Subir(contacto, zona, categoria, mensajes, cierre, ok, error),
                $"conversación con {contacto} ({mensajes.Count} mensajes)");
        }

        private void Anotar(MensajeGrabado mensaje)
        {
            if (!Enabled || _failed || _ended) return;
            _mensajes.Add(mensaje);
        }

        // ── Subida ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reproduce la cadena: NPC → chat → mensajes en orden → cierre.
        ///
        /// Va estrictamente en serie porque <c>ApiManager.NpcId</c> y <c>ChatId</c> son
        /// estado global: dos conversaciones a la vez se pisarían. Por eso la cola manda
        /// las entradas de familia Cadena de una en una, sin paralelismo.
        ///
        /// Un fallo a media cadena la aborta entera: no tiene sentido seguir metiendo
        /// mensajes en un chat que no se llegó a abrir.
        ///
        /// Cuando exista el endpoint de conversación completa esto pasa a ser un solo
        /// POST — misma grabación, mismo encolado, solo cambia este método.
        /// </summary>
        private static void Subir(string contacto, string zona, string categoria,
            List<MensajeGrabado> mensajes, string cierre, Action ok, Action<string> error)
        {
            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null) { error("No hay partida."); return; }

            api.RegistrarNPC(contacto, zona, "enemigo", confianza: 0,
                onSuccess: _ => api.IniciarChat(categoria,
                    onSuccess: _ => SubirMensaje(api, mensajes, 0, cierre, ok, error),
                    onError: error),
                onError: error);
        }

        /// <summary>Manda el mensaje <paramref name="i"/> y encadena el siguiente.</summary>
        private static void SubirMensaje(ApiManager api, List<MensajeGrabado> mensajes, int i,
            string cierre, Action ok, Action<string> error)
        {
            if (i >= mensajes.Count)
            {
                api.FinalizarChat(cierre, onSuccess: _ => ok(), onError: error);
                return;
            }

            var m = mensajes[i];
            Action<MensajeDto> siguiente = _ => SubirMensaje(api, mensajes, i + 1, cierre, ok, error);

            if (m.Tipo == "chain")
                api.RegistrarRespuestaJugador(m.Texto, m.Calidad, m.PreguntaBancoId,
                    onSuccess: siguiente, onError: error, opcionBancoId: m.OpcionBancoId);
            else
                api.RegistrarMensaje(m.Tipo, m.Texto, m.Calidad,
                    posiblesRespuestas: m.Opciones,
                    onSuccess: siguiente, onError: error,
                    preguntaBancoId: m.PreguntaBancoId, opcionBancoId: m.OpcionBancoId);
        }
    }
}
