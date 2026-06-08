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

        [Header("Modo local (sin servidor)")]
        [Tooltip("Si esta activo, NO se conecta al backend: simula todo localmente " +
                 "(usuarios en PlayerPrefs, partidas/NPCs/chats en memoria). " +
                 "Se activa automaticamente si el servidor no responde.")]
        [SerializeField] private bool useLocalMode = false;

        /// <summary>True si se esta simulando todo localmente (sin servidor).</summary>
        public bool IsLocalMode => useLocalMode;

        // ── Estado de sesion (en memoria) ───────────────────────────────────────
        public string Token { get; private set; }
        public int? UsuarioId { get; private set; }
        public int? PartidaId { get; private set; }
        public int? NpcId { get; private set; }
        public int? ChatId { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        // ── Estado local (solo se usa cuando useLocalMode = true) ────────────────
        private int _localPartidaSeq, _localNpcSeq, _localChatSeq, _localMsgSeq;
        private float _localProgreso;
        private int? _localNivelRiesgo;
        private readonly Dictionary<int, List<MensajeDto>> _localMensajes = new Dictionary<int, List<MensajeDto>>();

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
        // ║  CONECTIVIDAD                                                           ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Hace un ping a /health/ con timeout corto (4 s).
        /// Si el servidor no responde, activa el modo local automáticamente.
        /// onResult recibe true si el backend está disponible.
        /// </summary>
        public void CheckHealth(Action<bool> onResult)
        {
            if (useLocalMode) { onResult?.Invoke(false); return; }
            StartCoroutine(HealthRoutine(onResult));
        }

        private IEnumerator HealthRoutine(Action<bool> onResult)
        {
            string url = baseUrl + "/health/";
            using var req = UnityWebRequest.Get(url);
            req.timeout = 4;
            yield return req.SendWebRequest();

            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok)
            {
                Debug.LogWarning($"[API] Backend no disponible ({req.error}). " +
                                 "Activando modo local automáticamente.");
                useLocalMode = true;
            }
            else
            {
                Debug.Log($"[API] Backend disponible en {baseUrl}.");
            }
            onResult?.Invoke(ok);
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  AUTH                                                                  ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        public void Registro(string nombre, string password, Action onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalRegistro(nombre, password, onSuccess, onError); return; }

            var body = new { nombre, password };
            StartCoroutine(Send<AuthResponse>("POST", "/auth/registro/", body, auth: false,
                onSuccess: res => { StoreAuth(res); onSuccess?.Invoke(); },
                onError: onError));
        }

        public void Login(string nombre, string password, Action onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalLogin(nombre, password, onSuccess, onError); return; }

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
            if (useLocalMode) { LocalCrearPartida(progreso, nivelRiesgo, onSuccess); return; }

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
            if (useLocalMode) { LocalActualizarPartida(progreso, nivelRiesgo, onSuccess, onError); return; }

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
            if (useLocalMode) { LocalRegistrarNPC(nombre, area, tipo, confianza, onSuccess); return; }

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
            if (useLocalMode) { LocalActualizarConfianzaNPC(confianza, npcId, onSuccess, onError); return; }

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
            if (useLocalMode) { LocalIniciarChat(categoriaRiesgo, partidaId, npcId, onSuccess); return; }

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
        /// preguntaBancoId (opcional): ID de la pregunta del banco (ej: "HDU2_NPC01_F2_Q01").
        /// </summary>
        public void RegistrarMensaje(string tipo, string respuesta,
            string calidadRespuesta = "", List<OpcionRespuesta> posiblesRespuestas = null,
            Action<MensajeDto> onSuccess = null, Action<string> onError = null,
            string preguntaBancoId = null)
        {
            if (useLocalMode) { LocalRegistrarMensaje(tipo, respuesta, calidadRespuesta, posiblesRespuestas, onSuccess, onError, preguntaBancoId); return; }

            if (!RequireId(ChatId, "ChatId", onError)) return;

            var body = new Dictionary<string, object>
            {
                { "tipo", tipo },
                { "respuesta", respuesta },
            };
            if (!string.IsNullOrEmpty(calidadRespuesta))  body["calidad_respuesta"]  = calidadRespuesta;
            if (!string.IsNullOrEmpty(preguntaBancoId))   body["pregunta_banco_id"]  = preguntaBancoId;
            if (posiblesRespuestas != null && posiblesRespuestas.Count > 0) body["posibles_respuestas"] = posiblesRespuestas;

            StartCoroutine(Send<MensajeDto>("POST", $"/chats/{ChatId}/mensajes/registrar/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>Atajo para registrar la respuesta elegida por el jugador (tipo "chain").</summary>
        public void RegistrarRespuestaJugador(string textoElegido, string calidadRespuesta,
            string preguntaBancoId = null,
            Action<MensajeDto> onSuccess = null, Action<string> onError = null)
        {
            RegistrarMensaje("chain", textoElegido, calidadRespuesta, null, onSuccess, onError, preguntaBancoId);
        }

        /// <summary>Obtiene el historial completo de mensajes del chat activo.</summary>
        public void ObtenerHistorial(Action<List<MensajeDto>> onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalObtenerHistorial(onSuccess, onError); return; }

            if (!RequireId(ChatId, "ChatId", onError)) return;

            StartCoroutine(Send<List<MensajeDto>>("GET", $"/chats/{ChatId}/mensajes/", null, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>Cierra el chat activo (crea el mensaje "end" y marca fecha_termino).</summary>
        public void FinalizarChat(string mensajeCierre = "",
            Action<MensajeDto> onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalFinalizarChat(mensajeCierre, onSuccess, onError); return; }

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

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  MODO LOCAL (sin servidor) — simula el backend en memoria/PlayerPrefs  ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private static string LocalNow() => DateTime.UtcNow.ToString("o");
        private static string NewLocalToken() => "local-" + Guid.NewGuid().ToString("N").Substring(0, 16);

        private void LocalRegistro(string nombre, string password, Action onSuccess, Action<string> onError)
        {
            string key = "fishy.user." + nombre;
            if (PlayerPrefs.HasKey(key)) { onError?.Invoke("Ese usuario ya existe."); return; }

            int seq = PlayerPrefs.GetInt("fishy.userseq", 0) + 1;
            PlayerPrefs.SetInt("fishy.userseq", seq);
            PlayerPrefs.SetString(key, password);
            PlayerPrefs.SetInt("fishy.userid." + nombre, seq);
            PlayerPrefs.Save();

            Token = NewLocalToken();
            UsuarioId = seq;
            if (verboseLogs) Debug.Log($"[API-LOCAL] Registro '{nombre}' (id={seq}).");
            onSuccess?.Invoke();
        }

        private void LocalLogin(string nombre, string password, Action onSuccess, Action<string> onError)
        {
            string key = "fishy.user." + nombre;
            if (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetString(key) != password)
            {
                onError?.Invoke("Usuario o contraseña incorrectos.");
                return;
            }
            Token = NewLocalToken();
            UsuarioId = PlayerPrefs.GetInt("fishy.userid." + nombre, 1);
            if (verboseLogs) Debug.Log($"[API-LOCAL] Login '{nombre}' (id={UsuarioId}).");
            onSuccess?.Invoke();
        }

        private void LocalCrearPartida(float progreso, int? nivelRiesgo, Action<PartidaDto> onSuccess)
        {
            PartidaId = ++_localPartidaSeq;
            _localProgreso = progreso;
            _localNivelRiesgo = nivelRiesgo;
            if (verboseLogs) Debug.Log($"[API-LOCAL] Partida creada (id={PartidaId}).");
            onSuccess?.Invoke(BuildLocalPartida());
        }

        private void LocalActualizarPartida(float? progreso, int? nivelRiesgo, Action<PartidaDto> onSuccess, Action<string> onError)
        {
            if (!PartidaId.HasValue) { onError?.Invoke("No hay partida activa."); return; }
            if (progreso.HasValue) _localProgreso = progreso.Value;
            if (nivelRiesgo.HasValue) _localNivelRiesgo = nivelRiesgo.Value;
            onSuccess?.Invoke(BuildLocalPartida());
        }

        private PartidaDto BuildLocalPartida() => new PartidaDto
        {
            id = PartidaId ?? 0,
            usuario = UsuarioId ?? 0,
            progreso = _localProgreso,
            nivel_riesgo = _localNivelRiesgo,
            fecha_inicio = LocalNow(),
            fecha_update = LocalNow()
        };

        private void LocalRegistrarNPC(string nombre, string area, string tipo, int confianza, Action<NpcDto> onSuccess)
        {
            NpcId = ++_localNpcSeq;
            onSuccess?.Invoke(new NpcDto
            {
                id = NpcId.Value, partida = PartidaId ?? 0,
                nombre = nombre, area = area, tipo = tipo, confianza = confianza
            });
        }

        private void LocalActualizarConfianzaNPC(int confianza, int? npcId, Action<NpcDto> onSuccess, Action<string> onError)
        {
            int? id = npcId ?? NpcId;
            if (!id.HasValue) { onError?.Invoke("No hay NPC activo."); return; }
            onSuccess?.Invoke(new NpcDto { id = id.Value, partida = PartidaId ?? 0, confianza = confianza });
        }

        private void LocalIniciarChat(string categoriaRiesgo, int? partidaId, int? npcId, Action<ChatDto> onSuccess)
        {
            ChatId = ++_localChatSeq;
            _localMensajes[ChatId.Value] = new List<MensajeDto>();
            onSuccess?.Invoke(new ChatDto
            {
                id = ChatId.Value,
                partida = (partidaId ?? PartidaId) ?? 0,
                npc = (npcId ?? NpcId) ?? 0,
                categoria_riesgo = categoriaRiesgo,
                fecha_inicio = LocalNow()
            });
        }

        private void LocalRegistrarMensaje(string tipo, string respuesta, string calidadRespuesta,
            List<OpcionRespuesta> posibles, Action<MensajeDto> onSuccess, Action<string> onError,
            string preguntaBancoId = null)
        {
            if (!ChatId.HasValue) { onError?.Invoke("No hay chat activo."); return; }
            var dto = new MensajeDto
            {
                id = ++_localMsgSeq,
                chat = ChatId.Value,
                tipo = tipo,
                respuesta = respuesta,
                calidad_respuesta = calidadRespuesta,
                pregunta_banco_id = preguntaBancoId,
                timestamp = LocalNow(),
                posibles_respuestas = BuildLocalPosibles(posibles)
            };
            AddLocalMensaje(ChatId.Value, dto);
            if (verboseLogs)
                Debug.Log($"[API-LOCAL] Mensaje registrado — tipo={tipo} | calidad={calidadRespuesta} | pregunta={preguntaBancoId ?? "—"}");
            onSuccess?.Invoke(dto);
        }

        private void LocalObtenerHistorial(Action<List<MensajeDto>> onSuccess, Action<string> onError)
        {
            if (!ChatId.HasValue) { onError?.Invoke("No hay chat activo."); return; }
            onSuccess?.Invoke(_localMensajes.TryGetValue(ChatId.Value, out var list)
                ? new List<MensajeDto>(list) : new List<MensajeDto>());
        }

        private void LocalFinalizarChat(string mensajeCierre, Action<MensajeDto> onSuccess, Action<string> onError)
        {
            if (!ChatId.HasValue) { onError?.Invoke("No hay chat activo."); return; }
            var dto = new MensajeDto
            {
                id = ++_localMsgSeq, chat = ChatId.Value, tipo = "end",
                respuesta = mensajeCierre, timestamp = LocalNow()
            };
            AddLocalMensaje(ChatId.Value, dto);
            if (verboseLogs) Debug.Log($"[API-LOCAL] Chat {ChatId} finalizado.");
            ChatId = null;
            onSuccess?.Invoke(dto);
        }

        private void AddLocalMensaje(int chatId, MensajeDto dto)
        {
            if (!_localMensajes.TryGetValue(chatId, out var list))
            {
                list = new List<MensajeDto>();
                _localMensajes[chatId] = list;
            }
            list.Add(dto);
        }

        private static List<PosibleRespuestaDto> BuildLocalPosibles(List<OpcionRespuesta> opciones)
        {
            var list = new List<PosibleRespuestaDto>();
            if (opciones == null) return list;
            for (int i = 0; i < opciones.Count; i++)
                list.Add(new PosibleRespuestaDto
                {
                    id = i,
                    texto = opciones[i].texto,
                    orden = opciones[i].orden,
                    calidad_respuesta = opciones[i].calidad_respuesta
                });
            return list;
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
        public string pregunta_banco_id;   // ej. "HDU2_NPC01_F2_Q01"
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
