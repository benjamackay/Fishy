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
    /// Modelo de control parental: quien hace login es el ADULTO RESPONSABLE.
    /// Cada adulto administra uno o mas perfiles de menor (UsuarioJugador), que
    /// no tienen login propio, y la partida cuelga del perfil, no de la cuenta.
    /// El flujo completo es:
    ///
    ///     Login/Registro (adulto)  →  elegir perfil  →  continuar/crear partida
    ///
    /// Uso basico desde otro script:
    ///     ApiManager.Instance.Login("mama_ana", "1234",
    ///         onSuccess: () => ApiManager.Instance.ListarJugadores(
    ///             js => ApiManager.Instance.SeleccionarJugador(js[0].id)),
    ///         onError: err => Debug.LogError(err));
    ///
    /// Mantiene en memoria durante la sesion: Token, AdultoId, JugadorId,
    /// PartidaId, NpcId, ChatId.
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

        /// <summary>Id de la cuenta del adulto responsable (quien hizo login).</summary>
        public int? AdultoId { get; private set; }

        /// <summary>Id del perfil de menor activo. Se fija con SeleccionarJugador().</summary>
        public int? JugadorId { get; private set; }

        public int? PartidaId { get; private set; }
        public int? NpcId { get; private set; }
        public int? ChatId { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        /// <summary>True si ya se eligio el perfil de menor con el que se va a jugar.</summary>
        public bool HasJugador => JugadorId.HasValue;

        // ── Estado local (solo se usa cuando useLocalMode = true) ────────────────
        private int _localNpcSeq, _localChatSeq, _localMsgSeq;
        private string _localAdultoNombre, _localAdultoEmail;
        private PartidaDto _localPartida;   // partida activa en modo local
        private readonly Dictionary<int, List<MensajeDto>> _localMensajes = new Dictionary<int, List<MensajeDto>>();
        // A qué partida pertenece cada chat local: sin esto el riesgo por zona en
        // modo local mezclaría el avance de dos perfiles de menores distintos.
        private readonly Dictionary<int, int> _localChatPartida = new Dictionary<int, int>();

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

        /// <summary>
        /// Vuelve a preguntarle al backend si esta vivo, saliendo del modo local si
        /// se habia activado. Hace falta para poder REINTENTAR: una vez que
        /// HealthRoutine prende useLocalMode, CheckHealth corta antes de llegar a la
        /// red y siempre devuelve false, asi que la sesion se quedaria pegada en
        /// PlayerPrefs aunque el servidor ya hubiera vuelto.
        ///
        /// La usa la pantalla de ingreso, que exige backend real: sin el, los datos
        /// no llegarian a Supabase.
        /// </summary>
        public void ReintentarConexion(Action<bool> onResult)
        {
            useLocalMode = false;
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

        /// <summary>
        /// Crea la cuenta del adulto responsable. El email es OBLIGATORIO y unico
        /// (el backend rechaza el registro sin el). apellido/edad son opcionales.
        /// </summary>
        public void Registro(string nombre, string email, string password,
            string apellido = null, int? edad = null,
            Action onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalRegistro(nombre, email, password, onSuccess, onError); return; }

            var body = new Dictionary<string, object>
            {
                { "nombre", nombre },
                { "email", email },
                { "password", password },
            };
            if (!string.IsNullOrEmpty(apellido)) body["apellido"] = apellido;
            if (edad.HasValue)                   body["edad"]     = edad.Value;

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
            AdultoId = res.adulto_id;
            // Importante: una autenticación nueva pertenece (potencialmente) a otra
            // cuenta, así que descartamos perfil/partida/NPC/chat de la sesión anterior.
            // Si no lo hiciéramos, podríamos usar una PartidaId de otro adulto → el
            // backend devolvería 404 (get_object_or_404 con usuario_jugador__adulto=request.user).
            ResetSessionState();
        }

        /// <summary>
        /// Datos de la cuenta autenticada (GET /auth/perfil/).
        /// </summary>
        public void ObtenerPerfilAdulto(Action<AdultoResponsableDto> onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalObtenerPerfilAdulto(onSuccess, onError); return; }

            StartCoroutine(Send<AdultoResponsableDto>("GET", "/auth/perfil/", null, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>
        /// Limpia el estado de la sesión de juego (perfil de menor, partida, npc,
        /// chat) sin tocar el token. Se llama tras autenticarse para no arrastrar
        /// IDs de otra cuenta.
        /// </summary>
        public void ResetSessionState()
        {
            JugadorId = null;
            PartidaId = null;
            NpcId = null;
            ChatId = null;
            // En modo local los chats viven en memoria: si no se limpian, el riesgo
            // por zona de la cuenta anterior se arrastraría a la nueva sesión.
            _localMensajes.Clear();
            _localChatPartida.Clear();
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  PERFILES DE MENORES (control parental)                                ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>Lista los perfiles de menor del adulto autenticado.</summary>
        public void ListarJugadores(Action<List<UsuarioJugadorDto>> onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalListarJugadores(onSuccess); return; }

            StartCoroutine(Send<List<UsuarioJugadorDto>>("GET", "/jugadores/", null, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>
        /// Crea un perfil de menor. El nombre no se puede repetir dentro de la
        /// misma cuenta (el backend responde 400 con {"nombre": [...]}).
        /// </summary>
        public void CrearJugador(string nombre, int? edad = null,
            Action<UsuarioJugadorDto> onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalCrearJugador(nombre, edad, onSuccess, onError); return; }

            var body = new Dictionary<string, object> { { "nombre", nombre } };
            if (edad.HasValue) body["edad"] = edad.Value;

            StartCoroutine(Send<UsuarioJugadorDto>("POST", "/jugadores/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>Actualiza nombre y/o edad de un perfil de menor.</summary>
        public void ActualizarJugador(int jugadorId, string nombre = null, int? edad = null,
            Action<UsuarioJugadorDto> onSuccess = null, Action<string> onError = null)
        {
            if (useLocalMode) { LocalActualizarJugador(jugadorId, nombre, edad, onSuccess, onError); return; }

            var body = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(nombre)) body["nombre"] = nombre;
            if (edad.HasValue)                 body["edad"]   = edad.Value;

            StartCoroutine(Send<UsuarioJugadorDto>("PATCH", $"/jugadores/{jugadorId}/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>
        /// Borra un perfil de menor. Ojo: arrastra sus partidas en cascada.
        /// Si era el perfil activo, se limpia el estado de juego.
        /// </summary>
        public void EliminarJugador(int jugadorId, Action onSuccess = null, Action<string> onError = null)
        {
            Action despues = () =>
            {
                if (JugadorId == jugadorId) ResetSessionState();
                onSuccess?.Invoke();
            };

            if (useLocalMode) { LocalEliminarJugador(jugadorId, despues, onError); return; }

            StartCoroutine(Send<string>("DELETE", $"/jugadores/{jugadorId}/", null, auth: true,
                onSuccess: _ => despues(), onError: onError));
        }

        /// <summary>
        /// Fija el perfil de menor con el que se va a jugar. Descarta partida/NPC/chat
        /// del perfil anterior: cada menor tiene su propio avance y mezclarlos haría
        /// que un hermano escribiera en la partida del otro.
        /// </summary>
        public void SeleccionarJugador(int jugadorId)
        {
            if (JugadorId == jugadorId) return;
            ResetSessionState();
            JugadorId = jugadorId;
            if (verboseLogs) Debug.Log($"[API] Perfil de menor activo: {jugadorId}.");
        }

        /// <summary>
        /// Partidas de un perfil, de la más reciente a la más antigua.
        /// Es lo que permite retomar el avance entre sesiones.
        /// </summary>
        public void ObtenerPartidasJugador(int? jugadorId = null,
            Action<List<PartidaDto>> onSuccess = null, Action<string> onError = null)
        {
            int? id = jugadorId ?? JugadorId;
            if (!RequireId(id, "JugadorId", onError)) return;

            if (useLocalMode) { LocalObtenerPartidasJugador(id.Value, onSuccess); return; }

            StartCoroutine(Send<List<PartidaDto>>("GET", $"/jugadores/{id}/partidas/", null, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>
        /// Atajo para el flujo de "elegir perfil y jugar": retoma la última partida
        /// del menor y, si nunca ha jugado, le crea una. Deja PartidaId listo.
        /// El segundo parámetro del callback indica si la partida es nueva.
        /// </summary>
        public void ContinuarOCrearPartida(int? jugadorId = null,
            Action<PartidaDto, bool> onSuccess = null, Action<string> onError = null)
        {
            int? id = jugadorId ?? JugadorId;
            if (!RequireId(id, "JugadorId", onError)) return;

            SeleccionarJugador(id.Value);
            ObtenerPartidasJugador(id.Value,
                onSuccess: partidas =>
                {
                    if (partidas != null && partidas.Count > 0)
                    {
                        AdoptarPartida(partidas[0]);
                        if (verboseLogs) Debug.Log($"[API] Retomando partida {PartidaId} del perfil {id}.");
                        onSuccess?.Invoke(partidas[0], false);
                        return;
                    }
                    CrearPartida(id.Value, 0f, null,
                        onSuccess: p => onSuccess?.Invoke(p, true),
                        onError: onError);
                },
                onError: onError);
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  PARTIDA (HDU-2)                                                        ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Crea una partida para un perfil de menor (por defecto, el activo).
        /// Guarda PartidaId. El backend EXIGE el perfil: sin el responde 404.
        /// </summary>
        public void CrearPartida(int? jugadorId = null, float progreso = 0f, int? nivelRiesgo = null,
            Action<PartidaDto> onSuccess = null, Action<string> onError = null)
        {
            int? jId = jugadorId ?? JugadorId;
            if (!RequireId(jId, "JugadorId", onError)) return;

            if (useLocalMode) { LocalCrearPartida(jId.Value, progreso, nivelRiesgo, onSuccess); return; }

            var body = new Dictionary<string, object>
            {
                { "usuario_jugador_id", jId.Value },
                { "progreso", progreso },
            };
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
            string preguntaBancoId = null, string opcionBancoId = null)
        {
            if (useLocalMode) { LocalRegistrarMensaje(tipo, respuesta, calidadRespuesta, posiblesRespuestas, onSuccess, onError, preguntaBancoId, opcionBancoId); return; }

            if (!RequireId(ChatId, "ChatId", onError)) return;

            var body = new Dictionary<string, object>
            {
                { "tipo", tipo },
                { "respuesta", respuesta },
            };
            if (!string.IsNullOrEmpty(calidadRespuesta))  body["calidad_respuesta"]  = calidadRespuesta;
            if (!string.IsNullOrEmpty(preguntaBancoId))   body["pregunta_banco_id"]  = preguntaBancoId;
            if (!string.IsNullOrEmpty(opcionBancoId))     body["opcion_banco_id"]    = opcionBancoId;
            if (posiblesRespuestas != null && posiblesRespuestas.Count > 0) body["posibles_respuestas"] = posiblesRespuestas;

            StartCoroutine(Send<MensajeDto>("POST", $"/chats/{ChatId}/mensajes/registrar/", body, auth: true,
                onSuccess: onSuccess, onError: onError));
        }

        /// <summary>
        /// Atajo para registrar la respuesta elegida por el jugador (tipo "chain").
        ///
        /// <paramref name="opcionBancoId"/> es el id de la opción del banco (ej.
        /// "HDU2_NPC01_F2_Q01_R2"). Sin él la respuesta se guarda igual, pero NO
        /// cuenta para el riesgo por zona: el backend necesita la opción exacta
        /// para saber si valió -1, +1 o +2. <c>calidad_respuesta</c> no alcanza,
        /// porque no distingue una respuesta segura básica de una óptima.
        /// </summary>
        public void RegistrarRespuestaJugador(string textoElegido, string calidadRespuesta,
            string preguntaBancoId = null,
            Action<MensajeDto> onSuccess = null, Action<string> onError = null,
            string opcionBancoId = null)
        {
            RegistrarMensaje("chain", textoElegido, calidadRespuesta, null, onSuccess, onError,
                preguntaBancoId, opcionBancoId);
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
        // ║  RIESGO POR ZONA                                                        ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Riesgo acumulado de una partida, agrupado por zona del banco.
        ///
        /// Suma el impacto de cada opción que el menor eligió: insegura = -1,
        /// segura básica = +1, segura óptima = +2. <b>Más alto = más seguro.</b>
        /// Solo cuentan las respuestas registradas con su <c>opcion_banco_id</c>.
        /// </summary>
        public void ObtenerRiesgoPorZona(int? partidaId = null,
            Action<RiesgoPorZonaDto> onSuccess = null, Action<string> onError = null)
        {
            int? pId = partidaId ?? PartidaId;

            if (useLocalMode) { LocalRiesgoPorZona(pId, onSuccess, onError); return; }

            if (!RequireId(pId, "PartidaId", onError)) return;

            StartCoroutine(Send<RiesgoPorZonaDto>("GET", $"/partidas/{pId}/riesgo-por-zona/", null, auth: true,
                onSuccess: onSuccess, onError: onError));
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

        /// <summary>
        /// Marca una partida ya existente como la activa. En modo local también la
        /// deja cargada en memoria, o ActualizarPartida no sabría a cuál escribirle.
        /// </summary>
        private void AdoptarPartida(PartidaDto partida)
        {
            PartidaId = partida.id;
            if (useLocalMode) _localPartida = partida;
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

        // ── Persistencia local (PlayerPrefs + JSON) ──────────────────────────────
        // Los perfiles y sus partidas se guardan en disco, no solo en memoria: sin
        // eso el modo local no podría demostrar lo esencial de la feature, que es
        // que cada menor retoma SU avance entre sesiones.

        private static string JugadoresKey(int adultoId) => $"fishy.jugadores.{adultoId}";
        private static string PartidasKey(int jugadorId) => $"fishy.partidas.{jugadorId}";

        private static List<T> LoadLocalList<T>(string key)
        {
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw)) return new List<T>();
            try { return JsonConvert.DeserializeObject<List<T>>(raw) ?? new List<T>(); }
            catch { return new List<T>(); }
        }

        private static void SaveLocalList<T>(string key, List<T> list)
        {
            PlayerPrefs.SetString(key, JsonConvert.SerializeObject(list));
            PlayerPrefs.Save();
        }

        private void LocalRegistro(string nombre, string email, string password, Action onSuccess, Action<string> onError)
        {
            string key = "fishy.user." + nombre;
            if (PlayerPrefs.HasKey(key)) { onError?.Invoke("Ese usuario ya existe."); return; }
            // El backend exige email en el registro; el modo local valida igual para
            // que un formulario incompleto falle aquí y no solo contra el servidor.
            if (string.IsNullOrEmpty(email)) { onError?.Invoke("El email es obligatorio."); return; }

            int seq = PlayerPrefs.GetInt("fishy.userseq", 0) + 1;
            PlayerPrefs.SetInt("fishy.userseq", seq);
            PlayerPrefs.SetString(key, password);
            PlayerPrefs.SetInt("fishy.userid." + nombre, seq);
            PlayerPrefs.SetString("fishy.useremail." + nombre, email);
            PlayerPrefs.Save();

            Token = NewLocalToken();
            AdultoId = seq;
            _localAdultoNombre = nombre;
            _localAdultoEmail = email;
            ResetSessionState();
            if (verboseLogs) Debug.Log($"[API-LOCAL] Registro adulto '{nombre}' (id={seq}).");
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
            AdultoId = PlayerPrefs.GetInt("fishy.userid." + nombre, 1);
            _localAdultoNombre = nombre;
            _localAdultoEmail = PlayerPrefs.GetString("fishy.useremail." + nombre, "");
            ResetSessionState();
            if (verboseLogs) Debug.Log($"[API-LOCAL] Login adulto '{nombre}' (id={AdultoId}).");
            onSuccess?.Invoke();
        }

        private void LocalObtenerPerfilAdulto(Action<AdultoResponsableDto> onSuccess, Action<string> onError)
        {
            if (!AdultoId.HasValue) { onError?.Invoke("No hay sesión iniciada."); return; }
            onSuccess?.Invoke(new AdultoResponsableDto
            {
                id = AdultoId.Value,
                nombre = _localAdultoNombre,
                email = _localAdultoEmail,
                fecha_creacion = LocalNow()
            });
        }

        private void LocalListarJugadores(Action<List<UsuarioJugadorDto>> onSuccess)
        {
            onSuccess?.Invoke(LoadLocalList<UsuarioJugadorDto>(JugadoresKey(AdultoId ?? 0)));
        }

        private void LocalCrearJugador(string nombre, int? edad, Action<UsuarioJugadorDto> onSuccess, Action<string> onError)
        {
            if (!AdultoId.HasValue) { onError?.Invoke("No hay sesión iniciada."); return; }

            var lista = LoadLocalList<UsuarioJugadorDto>(JugadoresKey(AdultoId.Value));
            if (lista.Exists(j => j.nombre == nombre))
            {
                onError?.Invoke("Ya tienes un perfil con ese nombre.");
                return;
            }

            int seq = PlayerPrefs.GetInt("fishy.jugadorseq", 0) + 1;
            PlayerPrefs.SetInt("fishy.jugadorseq", seq);

            var dto = new UsuarioJugadorDto
            {
                id = seq,
                adulto = AdultoId.Value,
                nombre = nombre,
                edad = edad,
                fecha_creacion = LocalNow()
            };
            lista.Add(dto);
            SaveLocalList(JugadoresKey(AdultoId.Value), lista);

            if (verboseLogs) Debug.Log($"[API-LOCAL] Perfil de menor '{nombre}' creado (id={seq}).");
            onSuccess?.Invoke(dto);
        }

        private void LocalActualizarJugador(int jugadorId, string nombre, int? edad,
            Action<UsuarioJugadorDto> onSuccess, Action<string> onError)
        {
            if (!AdultoId.HasValue) { onError?.Invoke("No hay sesión iniciada."); return; }

            var lista = LoadLocalList<UsuarioJugadorDto>(JugadoresKey(AdultoId.Value));
            var jugador = lista.Find(j => j.id == jugadorId);
            if (jugador == null) { onError?.Invoke("Ese perfil no existe."); return; }

            if (!string.IsNullOrEmpty(nombre))
            {
                if (lista.Exists(j => j.id != jugadorId && j.nombre == nombre))
                {
                    onError?.Invoke("Ya tienes un perfil con ese nombre.");
                    return;
                }
                jugador.nombre = nombre;
            }
            if (edad.HasValue) jugador.edad = edad.Value;

            SaveLocalList(JugadoresKey(AdultoId.Value), lista);
            onSuccess?.Invoke(jugador);
        }

        private void LocalEliminarJugador(int jugadorId, Action onSuccess, Action<string> onError)
        {
            if (!AdultoId.HasValue) { onError?.Invoke("No hay sesión iniciada."); return; }

            var lista = LoadLocalList<UsuarioJugadorDto>(JugadoresKey(AdultoId.Value));
            if (lista.RemoveAll(j => j.id == jugadorId) == 0) { onError?.Invoke("Ese perfil no existe."); return; }

            SaveLocalList(JugadoresKey(AdultoId.Value), lista);
            PlayerPrefs.DeleteKey(PartidasKey(jugadorId));   // cascada, igual que el backend
            PlayerPrefs.Save();
            onSuccess?.Invoke();
        }

        private void LocalObtenerPartidasJugador(int jugadorId, Action<List<PartidaDto>> onSuccess)
        {
            var lista = LoadLocalList<PartidaDto>(PartidasKey(jugadorId));
            // fecha_update es ISO 8601 ("o"), así que ordena bien como texto.
            lista.Sort((a, b) => string.CompareOrdinal(b.fecha_update, a.fecha_update));
            onSuccess?.Invoke(lista);
        }

        private void LocalCrearPartida(int jugadorId, float progreso, int? nivelRiesgo, Action<PartidaDto> onSuccess)
        {
            int seq = PlayerPrefs.GetInt("fishy.partidaseq", 0) + 1;
            PlayerPrefs.SetInt("fishy.partidaseq", seq);

            var dto = new PartidaDto
            {
                id = seq,
                usuario_jugador = jugadorId,
                progreso = progreso,
                nivel_riesgo = nivelRiesgo,
                fecha_inicio = LocalNow(),
                fecha_update = LocalNow()
            };

            var lista = LoadLocalList<PartidaDto>(PartidasKey(jugadorId));
            lista.Add(dto);
            SaveLocalList(PartidasKey(jugadorId), lista);

            PartidaId = seq;
            _localPartida = dto;
            if (verboseLogs) Debug.Log($"[API-LOCAL] Partida creada (id={seq}) para el perfil {jugadorId}.");
            onSuccess?.Invoke(dto);
        }

        private void LocalActualizarPartida(float? progreso, int? nivelRiesgo, Action<PartidaDto> onSuccess, Action<string> onError)
        {
            if (!PartidaId.HasValue) { onError?.Invoke("No hay partida activa."); return; }
            if (_localPartida == null || _localPartida.id != PartidaId.Value)
            {
                onError?.Invoke("La partida activa no está cargada en modo local.");
                return;
            }

            if (progreso.HasValue)    _localPartida.progreso     = progreso.Value;
            if (nivelRiesgo.HasValue) _localPartida.nivel_riesgo = nivelRiesgo.Value;
            _localPartida.fecha_update = LocalNow();

            var lista = LoadLocalList<PartidaDto>(PartidasKey(_localPartida.usuario_jugador));
            int i = lista.FindIndex(p => p.id == _localPartida.id);
            if (i >= 0) lista[i] = _localPartida; else lista.Add(_localPartida);
            SaveLocalList(PartidasKey(_localPartida.usuario_jugador), lista);

            onSuccess?.Invoke(_localPartida);
        }

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
            _localChatPartida[ChatId.Value] = (partidaId ?? PartidaId) ?? 0;
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
            string preguntaBancoId = null, string opcionBancoId = null)
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
                opcion_banco_id = opcionBancoId,
                timestamp = LocalNow(),
                posibles_respuestas = BuildLocalPosibles(posibles)
            };
            AddLocalMensaje(ChatId.Value, dto);
            if (verboseLogs)
                Debug.Log($"[API-LOCAL] Mensaje registrado — tipo={tipo} | calidad={calidadRespuesta} | pregunta={preguntaBancoId ?? "—"}");
            onSuccess?.Invoke(dto);
        }

        /// <summary>
        /// Versión local del riesgo por zona: hace la misma cuenta que el backend,
        /// pero resolviendo el impacto contra <c>Resources/banco_preguntas.json</c>,
        /// que es la misma fuente que alimenta las conversaciones. Existe para que
        /// la demo sin servidor pueda mostrar la feature.
        /// </summary>
        private void LocalRiesgoPorZona(int? partidaId, Action<RiesgoPorZonaDto> onSuccess, Action<string> onError)
        {
            if (!RequireId(partidaId, "PartidaId", onError)) return;

            // Índice opcion_id -> (zona, impacto, peor, mejor) desde el banco.
            var banco = Fishy.Chat.BancoPreguntasLoader.Load();
            var indice = new Dictionary<string, RiesgoZonaDto>();
            var impactoDe = new Dictionary<string, int>();
            foreach (var pregunta in banco.preguntas)
            {
                if (pregunta.opciones_respuesta == null || pregunta.opciones_respuesta.Count == 0) continue;

                int peor = int.MaxValue, mejor = int.MinValue;
                foreach (var o in pregunta.opciones_respuesta)
                {
                    if (o.impacto_puntuacion < peor)  peor  = o.impacto_puntuacion;
                    if (o.impacto_puntuacion > mejor) mejor = o.impacto_puntuacion;
                }

                foreach (var o in pregunta.opciones_respuesta)
                {
                    if (string.IsNullOrEmpty(o.id)) continue;
                    indice[o.id] = new RiesgoZonaDto
                    {
                        zona = pregunta.zona,
                        minimo_posible = peor,
                        maximo_posible = mejor
                    };
                    impactoDe[o.id] = o.impacto_puntuacion;
                }
            }

            var resultado = new RiesgoPorZonaDto { partida_id = partidaId.Value };
            var porZona = new Dictionary<string, RiesgoZonaDto>();

            foreach (var kv in _localMensajes)
            {
                if (!_localChatPartida.TryGetValue(kv.Key, out int pid) || pid != partidaId.Value) continue;

                foreach (var mensaje in kv.Value)
                {
                    if (string.IsNullOrEmpty(mensaje.opcion_banco_id)) continue;
                    if (!indice.TryGetValue(mensaje.opcion_banco_id, out var meta))
                    {
                        resultado.sin_clasificar++;
                        continue;
                    }

                    if (!porZona.TryGetValue(meta.zona, out var acumulado))
                    {
                        acumulado = new RiesgoZonaDto { zona = meta.zona };
                        porZona[meta.zona] = acumulado;
                    }
                    acumulado.riesgo_acumulado += impactoDe[mensaje.opcion_banco_id];
                    acumulado.respuestas       += 1;
                    acumulado.minimo_posible   += meta.minimo_posible;
                    acumulado.maximo_posible   += meta.maximo_posible;
                    resultado.respuestas       += 1;
                    resultado.total            += impactoDe[mensaje.opcion_banco_id];
                }
            }

            resultado.zonas = new List<RiesgoZonaDto>(porZona.Values);
            resultado.zonas.Sort((a, b) => string.CompareOrdinal(a.zona, b.zona));
            onSuccess?.Invoke(resultado);
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
        /// <summary>Id de la CUENTA del adulto responsable (antes se llamaba usuario_id).</summary>
        public int adulto_id;
    }

    /// <summary>Cuenta del adulto responsable (GET /auth/perfil/).</summary>
    [Serializable]
    public class AdultoResponsableDto
    {
        public int id;
        public string nombre;
        public string apellido;
        public string email;
        public int? edad;
        public string fecha_nacimiento;
        public string fecha_creacion;
    }

    /// <summary>Perfil de menor. No tiene login: cuelga de un adulto responsable.</summary>
    [Serializable]
    public class UsuarioJugadorDto
    {
        public int id;
        public int adulto;
        public string nombre;
        public int? edad;
        public string fecha_creacion;
    }

    [Serializable]
    public class PartidaDto
    {
        public int id;
        /// <summary>Id del perfil de menor dueño de la partida (antes era `usuario`).</summary>
        public int usuario_jugador;
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
        public string opcion_banco_id;     // ej. "HDU2_NPC01_F2_Q01_R2"
        public string timestamp;
        public List<PosibleRespuestaDto> posibles_respuestas;
    }

    /// <summary>Riesgo acumulado de una zona. Más alto = más seguro.</summary>
    [Serializable]
    public class RiesgoZonaDto
    {
        public string zona;              // "desconocidos" | "chat_simulado"
        public int riesgo_acumulado;     // suma de impacto_puntuacion
        public int respuestas;           // cuántas respuestas se contaron
        public int minimo_posible;       // si hubiera elegido siempre lo peor
        public int maximo_posible;       // si hubiera elegido siempre lo mejor

        /// <summary>Posición en la escala 0..1 (0 = todo inseguro, 1 = todo óptimo).</summary>
        public float Normalizado
        {
            get
            {
                int rango = maximo_posible - minimo_posible;
                return rango == 0 ? 1f : (riesgo_acumulado - minimo_posible) / (float)rango;
            }
        }
    }

    [Serializable]
    public class RiesgoPorZonaDto
    {
        public int partida_id;
        public List<RiesgoZonaDto> zonas = new List<RiesgoZonaDto>();
        public int total;
        public int respuestas;
        public int sin_clasificar;       // respuestas cuya opción no está en el banco
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
