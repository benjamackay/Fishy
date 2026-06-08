using System;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Chat
{
    /// <summary>
    /// Registro best-effort de una sesión de chat en el backend (HDU-8 / celular diegético).
    ///
    /// Flujo interno:
    ///   Begin() → RegistrarNPC → IniciarChat → [ready=true] → Flush() de pendientes.
    ///
    /// Cada llamada a Log* encola una acción que se ejecuta tan pronto como el chat
    /// esté abierto en el backend. Si el backend no está disponible (modo local,
    /// sin sesión, o error de red), el módulo de chat sigue funcionando sin problema.
    ///
    /// El campo <c>preguntaBancoId</c> vincula cada mensaje con la pregunta del banco
    /// que lo originó (ej. "HDU2_NPC01_F2_Q01"), permitiendo análisis pedagógico.
    /// </summary>
    public class ChatBackendLogger
    {
        private bool _ready;
        private bool _failed;
        private readonly Queue<Action> _pending = new Queue<Action>();

        /// <summary>True si el logger está activo y el chat fue abierto en el backend.</summary>
        public bool Enabled { get; private set; }

        // ── Inicio de sesión ───────────────────────────────────────────────────
        /// <summary>
        /// Inicia el registro. Registra un NPC "enemigo" en la partida activa
        /// y abre el chat. Devuelve false si no hay sesión/partida disponible.
        /// </summary>
        public bool Begin(string contactName, string zoneId, string categoriaRiesgo)
        {
            var api = ApiManager.Instance;
            if (api == null || !api.IsLoggedIn || api.PartidaId == null)
            {
                _failed = true;
                return false;
            }

            Enabled = true;
            api.RegistrarNPC(contactName, zoneId, "enemigo", confianza: 0,
                onSuccess: _ => api.IniciarChat(categoriaRiesgo,
                    onSuccess: _ => { _ready = true; Flush(); },
                    onError:   Fail),
                onError: Fail);
            return true;
        }

        // ── Log de mensajes ────────────────────────────────────────────────────
        /// <summary>Mensaje inicial del NPC (tipo "start").</summary>
        public void LogStart(string npcText, string preguntaBancoId = null)
            => Enqueue(() => ApiManager.Instance.RegistrarMensaje(
                "start", npcText,
                preguntaBancoId: preguntaBancoId));

        /// <summary>Mensaje del NPC con opciones de respuesta (tipo "request").</summary>
        public void LogRequest(string npcText, List<OpcionRespuesta> opciones, string preguntaBancoId = null)
            => Enqueue(() => ApiManager.Instance.RegistrarMensaje(
                "request", npcText, "",
                posiblesRespuestas: opciones,
                preguntaBancoId: preguntaBancoId));

        /// <summary>Respuesta elegida por el jugador (tipo "chain").</summary>
        public void LogChoice(string playerText, string calidad, string preguntaBancoId = null)
            => Enqueue(() => ApiManager.Instance.RegistrarRespuestaJugador(
                playerText, calidad, preguntaBancoId));

        /// <summary>Cierra el chat en el backend (tipo "end").</summary>
        public void LogEnd(string mensajeCierre = "")
            => Enqueue(() => ApiManager.Instance.FinalizarChat(mensajeCierre));

        // ── Infraestructura ────────────────────────────────────────────────────
        private void Enqueue(Action action)
        {
            if (!Enabled || _failed) return;
            if (_ready) action();
            else        _pending.Enqueue(action);
        }

        private void Flush()
        {
            while (_pending.Count > 0) _pending.Dequeue()?.Invoke();
        }

        private void Fail(string error)
        {
            _failed = true;
            _pending.Clear();
            Debug.LogWarning($"[ChatBackendLogger] Registro deshabilitado: {error}");
        }
    }
}
