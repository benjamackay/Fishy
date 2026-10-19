using System;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Desconocidos
{
    /// <summary>
    /// Registro best-effort de la conversación de grooming en el backend (HDU-2).
    ///
    /// Encadena RegistrarNPC → IniciarChat y luego registra los mensajes
    /// (start / request / chain / end). Si el backend no está disponible o no hay
    /// sesión/partida, no registra nada y el juego sigue funcionando.
    ///
    /// El campo <c>preguntaBancoId</c> en cada llamada Log* vincula la respuesta
    /// con la pregunta del banco de preguntas (ej. "HDU2_NPC01_F2_Q01").
    /// </summary>
    public class GroomingChatLogger
    {
        private bool _ready;
        private bool _failed;
        private readonly Queue<Action> _pending = new Queue<Action>();

        public bool Enabled { get; private set; }

        // ── Inicio ─────────────────────────────────────────────────────────────
        /// <summary>Inicia el registro. Devuelve false si no hay backend disponible.</summary>
        public bool Begin(string npcName, string area, string categoriaRiesgo)
        {
            var api = ApiManager.Instance;
            if (api == null || !api.IsLoggedIn || api.PartidaId == null)
            {
                _failed = true;
                return false;
            }

            Enabled = true;
            api.RegistrarNPC(npcName, area, "enemigo", confianza: 0,
                onSuccess: _ => api.IniciarChat(categoriaRiesgo,
                    onSuccess: _ => { _ready = true; Flush(); },
                    onError:   e  => Fail(e)),
                onError: e => Fail(e));
            return true;
        }

        // ── Log de mensajes ────────────────────────────────────────────────────
        public void LogStart(string npcLine, string preguntaBancoId = null)
            => Enqueue(() => ApiManager.Instance.RegistrarMensaje(
                "start", npcLine,
                preguntaBancoId: preguntaBancoId));

        public void LogRequest(string npcLine, List<OpcionRespuesta> opciones, string preguntaBancoId = null)
            => Enqueue(() => ApiManager.Instance.RegistrarMensaje(
                "request", npcLine, "",
                posiblesRespuestas: opciones,
                preguntaBancoId: preguntaBancoId));

        public void LogChoice(string texto, string calidad, string preguntaBancoId = null)
            => Enqueue(() => ApiManager.Instance.RegistrarRespuestaJugador(
                texto, calidad, preguntaBancoId));

        /// <summary>
        /// Registra el desenlace de la interacción actualizando la confianza del NPC
        /// en el backend: <c>0</c> = el niño/a NO compartió datos (a salvo),
        /// <c>100</c> = fue manipulado hasta el final (captura).
        ///
        /// Como estos NPCs son los gatillantes para abrir otras zonas, esto deja un
        /// registro estructurado y consultable del resultado de cada interacción.
        /// </summary>
        public void LogOutcome(bool success)
            => Enqueue(() => ApiManager.Instance.ActualizarConfianzaNPC(success ? 0 : 100));

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
            Debug.LogWarning($"[GroomingChatLogger] Registro deshabilitado: {error}");
        }
    }
}
