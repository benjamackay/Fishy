using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Fishy.Net
{
    /// <summary>
    /// Punto unico de comunicacion entre Unity y el backend Django de Fishy!.
    /// Cubre HDU-2 (partida + NPCs) y HDU-8 (chat simulado).
    ///
    /// Uso basico desde otro script:
    ///     ApiManager.Instance.Login("jugador_test", "1234",
    ///         onSuccess: () => Debug.Log("logueado"),
    ///         onError:   err => Debug.LogError(err));
    ///
    /// Mantiene en memoria durante la sesion: Token, UsuarioId, PartidaId, NpcId, ChatId.
    /// </summary>
    public class ApiManager : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────────────────────────────
        public static ApiManager Instance { get; private set; }

        [Header("Configuracion")]
        [Tooltip("URL base del backend Django, sin slash final. Ej: http://127.0.0.1:8000/api")]
        [SerializeField] private string baseUrl = "http://127.0.0.1:8000/api";

        [Tooltip("Timeout por peticion en segundos.")]
        [SerializeField] private int timeoutSeconds = 15;

        [Tooltip("Mostrar en consola el detalle de cada peticion/respuesta.")]
        [SerializeField] private bool verboseLogs = true;

        // ── Estado de sesion (en memoria) ───────────────────────────────────────
        public string Token { get; private set; }
        public int? UsuarioId { get; private set; }
        public int? PartidaId { get; private set; }
        public int? NpcId { get; private set; }
        public int? ChatId { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  AUTH                                                                  ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        public void Registro(string nombre, string password, Action onSuccess = null, Action<string> onError = null)
        {
            var body = new { nombre, password };
            StartCoroutine(Send<AuthResponse>("POST", "/auth/registro/", body, auth: false,
                onSuccess: res => { StoreAuth(res); onSuccess?.Invoke(); },
                onError: onError));
        }

        public void Login(string nombre, string password, Action onSuccess = null, Action<string> onError = null)
        {
            var body = new { nombre, password };
            StartCoroutine(Send<AuthResponse>("POST", "/auth/login/", body, auth: false,
                onSuccess: res => { StoreAuth(res); onSuccess?.Invoke(); },
                onError: onError));
        }

        private void StoreAuth(AuthResponse res)
        {
            Token = res.token;
            UsuarioId = res.usuario_id;
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  PARTIDA (HDU-2)                                                        ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>Crea una partida para el usuario logueado. Guarda PartidaId.</summary>
        public void CrearPartida(float progreso = 0f, int? nivelRiesgo = null,
            Action<PartidaDto> onSuccess = null, Action<string> onError = null)
        {
            var body = new Dictionary<string, object> { { "progreso", progreso } };
            if (nivelRiesgo.HasValue) body["nivel_riesgo"] = nivelRiesgo.Value;

            StartCoroutine(Send<PartidaDto>("POST", "/partidas/", body, auth: true,
                onSuccess: res => { PartidaId = res.id; onSuccess?.Invoke(res); },
                onError: onError));
        }

        /// <summary>Actualiza progreso (0-100) y/o nivel_riesgo de la partida activa.</summary>
        public void ActualizarPartida(float? progreso = null, int? nivelRiesgo = null,
            Action<PartidaDto> onSuccess = null, Action<string> onError = null)
        {
            if (!RequireId(PartidaId, "PartidaId", onError)) return;

            var body = new Dictionary<string, object>();
            if (progreso.HasValue) body["progreso"] = progreso.Value;
            if (nivelRiesgo.HasValue) body["nivel_riesgo"] = nivelRiesgo.Value;

            StartCoroutine(Send<PartidaDto>("PATCH", $"/partidas/{PartidaId}/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  NPC (HDU-2)                                                            ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Registra un NPC en la partida activa. Guarda NpcId.
        /// tipo: "aliado" | "neutral" | "enemigo".
        /// </summary>
        public void RegistrarNPC(string nombre, string area, string tipo = "neutral", int confianza = 0,
            Action<NpcDto> onSuccess = null, Action<string> onError = null)
        {
            if (!RequireId(PartidaId, "PartidaId", onError)) return;

            var body = new { nombre, area, tipo, confianza };
            StartCoroutine(Send<NpcDto>("POST", $"/partidas/{PartidaId}/npcs/", body, auth: true,
                onSuccess: res => { NpcId = res.id; onSuccess?.Invoke(res); },
                onError: onError));
        }

        /// <summary>Actualiza la confianza del NPC indicado (por defecto, el NpcId activo).</summary>
        public void ActualizarConfianzaNPC(int confianza, int? npcId = null,
            Action<NpcDto> onSuccess = null, Action<string> onError = null)
        {
            int? id = npcId ?? NpcId;
            if (!RequireId(id, "NpcId", onError)) return;

            var body = new { confianza };
            StartCoroutine(Send<NpcDto>("PATCH", $"/npcs/{id}/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  CHAT (HDU-8)                                                           ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Inicia un chat para la partida + NPC activos. Guarda ChatId.
        /// categoriaRiesgo: ej "grooming", "ciberacoso", "retos_virales".
        /// </summary>
        public void IniciarChat(string categoriaRiesgo = "", int? partidaId = null, int? npcId = null,
            Action<ChatDto> onSuccess = null, Action<string> onError = null)
        {
            int? pId = partidaId ?? PartidaId;
            int? nId = npcId ?? NpcId;
            if (!RequireId(pId, "PartidaId", onError)) return;
            if (!RequireId(nId, "NpcId", onError)) return;

            var body = new { partida_id = pId.Value, npc_id = nId.Value, categoria_riesgo = categoriaRiesgo };
            StartCoroutine(Send<ChatDto>("POST", "/chats/", body, auth: true,
                onSuccess: res => { ChatId = res.id; onSuccess?.Invoke(res); },
                onError: onError));
        }

        /// <summary>
        /// Registra un mensaje en el chat activo.
        /// tipo: "start" (NPC neutro) | "request" (NPC con riesgo + opciones) | "chain" (respuesta del jugador).
        /// El tipo "end" se crea automaticamente con FinalizarChat().
        /// calidadRespuesta (opcional): "buena" | "neutral" | "mala".
        /// </summary>
        public void RegistrarMensaje(string tipo, string respuesta,
            string calidadRespuesta = "", List<OpcionRespuesta> posiblesRespuestas = null,
            Action<MensajeDto> onSuccess = null, Action<string> onError = null)
        {
            if (!RequireId(ChatId, "ChatId", onError)) return;

            var body = new Dictionary<string, object>
            {
                { "tipo", tipo },
                { "respuesta", respuesta },
            };
            if (!string.IsNullOrEmpty(calidadRespuesta)) body["calidad_respuesta"] = calidadRespuesta;
            if (posiblesRespuestas != null && posiblesRespuestas.Count > 0) body["posibles_respuestas"] = posiblesRespuestas;

            StartCoroutine(Send<MensajeDto>("POST", $"/chats/{ChatId}/mensajes/registrar/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>Atajo para registrar la respuesta elegida por el jugador (tipo "chain").</summary>
        public void RegistrarRespuestaJugador(string textoElegido, string calidadRespuesta,
            Action<MensajeDto> onSuccess = null, Action<string> onError = null)
        {
            RegistrarMensaje("chain", textoElegido, calidadRespuesta, null, onSuccess, onError);
        }

        /// <summary>Obtiene el historial completo de mensajes del chat activo.</summary>
        public void ObtenerHistorial(Action<List<MensajeDto>> onSuccess = null, Action<string> onError = null)
        {
            if (!RequireId(ChatId, "ChatId", onError)) return;

            StartCoroutine(Send<List<MensajeDto>>("GET", $"/chats/{ChatId}/mensajes/", null, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  BANCO DE PREGUNTAS (HDU-2 / HDU-8)                                    ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Obtiene preguntas del banco filtradas. Todos los parámetros son opcionales.
        /// zona: "desconocidos" | "chat_simulado"
        /// npcId: "NPC_01" | "NPC_02"
        /// escenarioId: "CHAT_GROOMING_01" | "CHAT_CIBERACOSO_01" | "CHAT_RETO_VIRAL_01"
        /// hdu: "HDU-2" | "HDU-8"
        /// fase: número de fase (solo HDU-2)
        /// soloRiesgo: si true, devuelve solo mensajes que requieren respuesta del jugador
        /// </summary>
        public void ObtenerPreguntas(
            string zona = "", string npcId = "", string escenarioId = "",
            string hdu = "", int? fase = null, bool soloRiesgo = false,
            Action<List<PreguntaDto>> onSuccess = null, Action<string> onError = null)
        {
            var query = new System.Text.StringBuilder("/banco/preguntas/?");
            if (!string.IsNullOrEmpty(zona))        query.Append($"zona={Uri.EscapeDataString(zona)}&");
            if (!string.IsNullOrEmpty(npcId))       query.Append($"npc_id={Uri.EscapeDataString(npcId)}&");
            if (!string.IsNullOrEmpty(escenarioId)) query.Append($"escenario_id={Uri.EscapeDataString(escenarioId)}&");
            if (!string.IsNullOrEmpty(hdu))         query.Append($"hdu={Uri.EscapeDataString(hdu)}&");
            if (fase.HasValue)                      query.Append($"fase={fase.Value}&");
            if (soloRiesgo)                         query.Append("solo_riesgo=true&");

            StartCoroutine(Send<List<PreguntaDto>>("GET", query.ToString().TrimEnd('&', '?'), null, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>Obtiene una pregunta concreta por su pregunta_id (ej: "HDU2_NPC01_F2_Q01").</summary>
        public void ObtenerPregunta(string preguntaId,
            Action<PreguntaDto> onSuccess = null, Action<string> onError = null)
        {
            StartCoroutine(Send<PreguntaDto>("GET", $"/banco/preguntas/{Uri.EscapeDataString(preguntaId)}/",
                null, auth: true, onSuccess: onSuccess, onError: onError));
        }

        /// <summary>Cierra el chat activo (crea el mensaje "end" y marca fecha_termino).</summary>
        public void FinalizarChat(string mensajeCierre = "",
            Action<MensajeDto> onSuccess = null, Action<string> onError = null)
        {
            if (!RequireId(ChatId, "ChatId", onError)) return;

            var body = new { respuesta = mensajeCierre };
            int? closedChat = ChatId;
            StartCoroutine(Send<MensajeDto>("POST", $"/chats/{closedChat}/finalizar/", body, auth: true,
                onSuccess: res => { ChatId = null; onSuccess?.Invoke(res); },
                onError: onError));
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  NUCLEO HTTP                                                            ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private IEnumerator Send<TResponse>(string method, string path, object body, bool auth,
            Action<TResponse> onSuccess, Action<string> onError)
        {
            string url = baseUrl + path;

            using var req = new UnityWebRequest(url, method);
            req.timeout = timeoutSeconds;
            req.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                byte[] raw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.SetRequestHeader("Content-Type", "application/json");
                if (verboseLogs) Debug.Log($"[API] {method} {url}\n{json}");
            }
            else if (verboseLogs)
            {
                Debug.Log($"[API] {method} {url}");
            }

            if (auth)
            {
                if (!IsLoggedIn)
                {
                    onError?.Invoke("No hay token: debes hacer Login/Registro primero.");
                    yield break;
                }
                req.SetRequestHeader("Authorization", $"Token {Token}");
            }

            yield return req.SendWebRequest();

            string text = req.downloadHandler != null ? req.downloadHandler.text : "";

            if (req.result != UnityWebRequest.Result.Success)
            {
                string msg = $"[API] Error {(int)req.responseCode} en {method} {path}: {req.error}\n{text}";
                if (verboseLogs) Debug.LogError(msg);
                onError?.Invoke(string.IsNullOrEmpty(text) ? req.error : text);
                yield break;
            }

            if (verboseLogs) Debug.Log($"[API] OK {(int)req.responseCode} {path}\n{text}");

            TResponse parsed = default;
            if (!string.IsNullOrEmpty(text) && typeof(TResponse) != typeof(string))
            {
                try
                {
                    parsed = JsonConvert.DeserializeObject<TResponse>(text);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Error al parsear JSON: {e.Message}\n{text}");
                    yield break;
                }
            }
            else if (typeof(TResponse) == typeof(string))
            {
                parsed = (TResponse)(object)text;
            }

            onSuccess?.Invoke(parsed);
        }

        private bool RequireId(int? id, string name, Action<string> onError)
        {
            if (id.HasValue) return true;
            onError?.Invoke($"Falta {name} en la sesion. Asegurate de crear/registrar antes de esta llamada.");
            return false;
        }
    }

    // ╔═══════════════════════════════════════════════════════════════════════════╗
    // ║  DTOs  (coinciden con los serializers de Django)                           ║
    // ╚═══════════════════════════════════════════════════════════════════════════╝

    [Serializable]
    public class AuthResponse
    {
        public string token;
        public int usuario_id;
    }

    [Serializable]
    public class PartidaDto
    {
        public int id;
        public int usuario;
        public int? nivel_riesgo;
        public float progreso;
        public string fecha_inicio;
        public string fecha_update;
    }

    [Serializable]
    public class NpcDto
    {
        public int id;
        public int partida;
        public string nombre;
        public string area;
        public string tipo;       // aliado | neutral | enemigo
        public int confianza;
    }

    [Serializable]
    public class ChatDto
    {
        public int id;
        public int partida;
        public int npc;
        public string categoria_riesgo;
        public string fecha_inicio;
        public string fecha_termino;
    }

    [Serializable]
    public class MensajeDto
    {
        public int id;
        public int chat;
        public string tipo;                // start | chain | request | end
        public string respuesta;
        public string calidad_respuesta;   // buena | neutral | mala
        public string timestamp;
        public List<PosibleRespuestaDto> posibles_respuestas;
    }

    [Serializable]
    public class PosibleRespuestaDto
    {
        public int id;
        public string texto;
        public int orden;
        public string calidad_respuesta;
    }

    [Serializable]
    public class OpcionBancoDto
    {
        public int id;
        public string opcion_id;
        public string texto;
        public string tipo;                    // insegura | segura_basica | segura_optima
        public string consecuencia_narrativa;
        public int impacto_puntuacion;
        public string siguiente_pregunta;      // null si es el final de la rama
        public int orden;
    }

    [Serializable]
    public class PreguntaDto
    {
        public int id;
        public string pregunta_id;
        public string hdu;
        public string zona;
        // HDU-2
        public string npc_id;
        public string npc_nombre;
        public string npc_avatar;
        public int? fase;
        public int? orden_en_fase;
        public string narrativa_continuacion;
        // HDU-8
        public string escenario_id;
        public string escenario_nombre;
        public List<object> historial_previo;
        // Comunes
        public string categoria;
        public int nivel_riesgo;
        public bool es_mensaje_riesgo;
        public string mensaje_npc;
        public List<string> etiquetas_ml;
        public List<OpcionBancoDto> opciones;
    }

    /// <summary>Opcion de respuesta que se envia (NO incluye id; el backend lo genera).</summary>
    [Serializable]
    public class OpcionRespuesta
    {
        public string texto;
        public int orden;
        public string calidad_respuesta;   // buena | neutral | mala

        public OpcionRespuesta() { }

        public OpcionRespuesta(string texto, int orden, string calidadRespuesta)
        {
            this.texto = texto;
            this.orden = orden;
            this.calidad_respuesta = calidadRespuesta;
        }
    }
}
