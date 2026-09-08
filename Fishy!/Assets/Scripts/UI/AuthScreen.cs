using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fishy.Net;

namespace Fishy.UI
{
    /// <summary>
    /// Pantalla de acceso previa al juego, en dos pasos (modelo de control parental):
    ///
    ///   PASO 1 — Cuenta del adulto responsable
    ///     • "Entrar"       → login con nombre + contraseña.
    ///     • "Crear cuenta" → registro con nombre + email + contraseña.
    ///     El email es obligatorio al registrar porque el backend lo exige y es
    ///     único por cuenta. Por eso los dos modos están separados: antes había un
    ///     "login inteligente" que registraba solo si el login fallaba, y eso ya no
    ///     es posible sin pedir el email siempre.
    ///
    ///   PASO 2 — Perfil de menor
    ///     El adulto elige cuál de sus hijos va a jugar (o crea el primero). Recién
    ///     ahí se retoma la partida de ESE menor, o se le crea una si nunca jugó.
    ///     Cada hermano conserva su propio avance.
    ///
    /// Al arrancar hace un ping al backend (CheckHealth). Si el servidor no
    /// responde, activa el modo local (PlayerPrefs) de forma automática y el
    /// juego sigue funcionando sin conexión, perfiles incluidos.
    /// </summary>
    public class AuthScreen : MonoBehaviour
    {
        public enum Mode { Login, Register }

        [Header("Destino")]
        [Tooltip("Nombre de la escena del juego (debe estar en File → Build Settings).")]
        public string gameSceneName = "SampleScene";
        [Tooltip("Pantalla de carga. Si está vacía se busca en la escena.")]
        public LoadingScreen loadingScreen;

        [Header("Sesión")]
        [Tooltip("Modo inicial al abrir la pantalla.")]
        public Mode mode = Mode.Login;
        [Tooltip("Pedir el perfil de menor y preparar su partida tras autenticarse. " +
                 "Si se desactiva, entra al juego sin partida (solo para pruebas).")]
        public bool crearPartidaAlIngresar = true;

        [Header("Auto-login de pruebas (dejar 'Auto Login Nombre' vacío para desactivar)")]
        [Tooltip("Si tiene texto, al arrancar se loguea solo con estas credenciales " +
                 "(las crea si no existen) y se salta la pantalla de login por completo. " +
                 "Vacíalo para volver al login manual normal — nada más cambia.")]
        [SerializeField] private string autoLoginNombre = "";
        [SerializeField] private string autoLoginPassword = "";
        [Tooltip("Perfil de menor a usar/crear automáticamente. Solo aplica si 'Auto Login Nombre' no está vacío.")]
        [SerializeField] private string autoLoginPerfil = "TesterQA";

        [Header("Referencias (opcionales; se generan en runtime)")]
        public GameObject panel;
        public Text titleLabel;
        public InputField usernameField;
        public InputField emailField;
        public InputField passwordField;
        public Button submitButton;
        public Text submitLabel;
        public Button toggleModeButton;
        public Text toggleModeLabel;
        public Text statusLabel;
        public Text connectionBadge;   // indicador de conexión (generado en runtime)

        [Header("Selección de perfil (se genera en runtime)")]
        public GameObject profilePanel;
        public Transform profileListContainer;
        public InputField newProfileNameField;
        public InputField newProfileAgeField;
        public Button createProfileButton;
        public Text profileStatusLabel;

        [Header("Continuar partida (se genera en runtime)")]
        [Tooltip("Cuántas sesiones se ofrecen para continuar. El backend las manda de la " +
                 "más reciente a la más antigua, así que se muestran las N últimas.")]
        [Min(1)] public int maxPartidasEnLista = 3;
        public GameObject savePanel;
        public Transform saveListContainer;
        public Button newGameButton;
        public Button backToProfilesButton;
        public Text saveStatusLabel;

        private Font font;
        private bool busy;
        private bool profileBusy;
        private bool saveBusy;
        private bool backendReady;

        /// <summary>Perfil elegido en el paso 2. El paso 3 lo necesita para crear una
        /// partida nueva si el menor no quiere continuar ninguna de las guardadas.</summary>
        private UsuarioJugadorDto perfilElegido;

        // ── Ciclo de vida ──────────────────────────────────────────────────────
        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UiBootstrap.EnsureEventSystem();
            EnsureApiManager();

            if (loadingScreen == null) loadingScreen = FindAnyObjectByType<LoadingScreen>();

            // La UI se construye SIEMPRE antes de decidir el flujo: aunque ya haya
            // sesión, puede faltar elegir el perfil y ahí necesitamos el panel.
            if (panel == null) BuildRuntimeUI();

            if (submitButton        != null) submitButton.onClick.AddListener(Submit);
            if (toggleModeButton    != null) toggleModeButton.onClick.AddListener(ToggleMode);
            if (createProfileButton != null) createProfileButton.onClick.AddListener(CreateProfile);
            if (newGameButton        != null) newGameButton.onClick.AddListener(EmpezarPartidaNueva);
            if (backToProfilesButton != null) backToProfilesButton.onClick.AddListener(ShowProfiles);

            ApplyMode();

            // Si el ApiManager ya tiene sesión activa (DontDestroyOnLoad entre
            // escenas), saltamos el login y seguimos donde corresponda.
            if (ApiManager.Instance.IsLoggedIn)
            {
                OnAuthSuccess();
                return;
            }

            bool autoLogin = !string.IsNullOrEmpty(autoLoginNombre);
            if (!autoLogin) Show();   // en auto-login no se muestra la UI en absoluto

            // Bloquear formulario mientras se verifica la conexión con el backend.
            // Esto también decide useLocalMode, así que el auto-login funciona
            // igual de bien sin servidor (cae al modo local simulado).
            SetBusy(true);
            SetStatus("Conectando con el servidor…", false);

            ApiManager.Instance.CheckHealth(ok =>
            {
                backendReady = ok;
                SetBusy(false);
                UpdateConnectionBadge(ok);
                SetStatus("", false);

                if (autoLogin) IniciarAutoLogin();
            });
        }

        // ── Auto-login de pruebas ──────────────────────────────────────────────
        private void IniciarAutoLogin()
        {
            SetBusy(true);
            SetStatus("Entrando con cuenta de pruebas…", false);
            Debug.Log($"[AuthScreen] Auto-login de pruebas activo ('{autoLoginNombre}'). " +
                      "Vacía 'Auto Login Nombre' en el Inspector para volver al login manual.");

            ApiManager.Instance.Login(autoLoginNombre, autoLoginPassword,
                onSuccess: OnAutoLoginListo,
                onError: _ =>
                {
                    // Cuenta de pruebas todavía no existe: la crea. El email es
                    // inventado a partir del nombre — no importa para pruebas, el
                    // backend solo exige que sea único.
                    string email = $"{autoLoginNombre.ToLowerInvariant()}@pruebas.fishygame.cl";
                    ApiManager.Instance.Registro(autoLoginNombre, email, autoLoginPassword,
                        onSuccess: OnAutoLoginListo,
                        onError: err =>
                        {
                            SetBusy(false);
                            SetStatus("Auto-login de pruebas falló: " + FriendlyError(err), true);
                            Show();   // recién acá se muestra la UI, como respaldo
                        });
                });
        }

        private void OnAutoLoginListo()
        {
            if (!crearPartidaAlIngresar) { StartGame(); return; }

            ApiManager.Instance.ListarJugadores(
                onSuccess: jugadores =>
                {
                    // preguntar: false — el auto-login existe para NO ver ninguna pantalla.
                    // Si se detuviera en el selector de partidas dejaría de saltarse el
                    // acceso, que es justo lo único que promete.
                    var existente = jugadores?.Find(j => j.nombre == autoLoginPerfil);
                    if (existente != null) { ChooseProfile(existente, preguntar: false); return; }

                    ApiManager.Instance.CrearJugador(autoLoginPerfil, null,
                        onSuccess: nuevo => ChooseProfile(nuevo, preguntar: false),
                        onError: err =>
                        {
                            SetBusy(false);
                            SetStatus(FriendlyError(err), true);
                            Show();
                        });
                },
                onError: err =>
                {
                    SetBusy(false);
                    SetStatus(FriendlyError(err), true);
                    Show();
                });
        }

        // ── Visibilidad ────────────────────────────────────────────────────────
        public void Show()
        {
            if (panel        != null) panel.SetActive(true);
            if (profilePanel != null) profilePanel.SetActive(false);
            if (savePanel    != null) savePanel.SetActive(false);
        }

        public void Hide()
        {
            if (panel        != null) panel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(false);
            if (savePanel    != null) savePanel.SetActive(false);
        }

        // ── Cambio de modo ─────────────────────────────────────────────────────
        public void ToggleMode()
        {
            mode = mode == Mode.Login ? Mode.Register : Mode.Login;
            ApplyMode();
        }

        private void ApplyMode()
        {
            bool login = mode == Mode.Login;
            if (titleLabel  != null) titleLabel.text  = login ? "Iniciar sesión" : "Crear cuenta de adulto";
            if (submitLabel != null) submitLabel.text = login ? "Entrar"         : "Registrarme";
            if (toggleModeLabel != null)
                toggleModeLabel.text = login
                    ? "¿Primera vez? Crear cuenta"
                    : "¿Ya tienes cuenta? Inicia sesión";

            // El email solo se pide al registrar: para entrar basta nombre + contraseña.
            if (emailField != null) emailField.gameObject.SetActive(!login);

            SetStatus("", false);
        }

        // ── Paso 1: cuenta del adulto ──────────────────────────────────────────
        public void Submit()
        {
            if (busy) return;

            string user  = usernameField != null ? usernameField.text.Trim() : "";
            string pass  = passwordField != null ? passwordField.text        : "";
            string email = emailField    != null ? emailField.text.Trim()    : "";

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                SetStatus("Escribe tu nombre y tu contraseña.", true);
                return;
            }
            if (pass.Length < 4)
            {
                SetStatus("La contraseña debe tener al menos 4 caracteres.", true);
                return;
            }

            if (mode == Mode.Register)
            {
                if (string.IsNullOrEmpty(email))
                {
                    SetStatus("Necesitamos un email para crear la cuenta.", true);
                    return;
                }
                if (!LooksLikeEmail(email))
                {
                    SetStatus("Ese email no parece válido.", true);
                    return;
                }

                SetBusy(true);
                SetStatus("Creando cuenta…", false);
                ApiManager.Instance.Registro(user, email, pass,
                    onSuccess: OnAuthSuccess,
                    onError:   err => { SetBusy(false); SetStatus(FriendlyError(err), true); });
                return;
            }

            SetBusy(true);
            SetStatus("Ingresando…", false);
            ApiManager.Instance.Login(user, pass,
                onSuccess: OnAuthSuccess,
                onError:   err => { SetBusy(false); SetStatus(FriendlyError(err), true); });
        }

        /// <summary>Validación mínima de forma; la de verdad la hace el backend.</summary>
        private static bool LooksLikeEmail(string value)
        {
            int at = value.IndexOf('@');
            return at > 0 && value.IndexOf('.', at) > at + 1 && !value.EndsWith(".");
        }

        // ── Éxito de autenticación → paso 2 ────────────────────────────────────
        private void OnAuthSuccess()
        {
            SetBusy(false);

            if (!crearPartidaAlIngresar)
            {
                StartGame();
                return;
            }

            // Si ya se eligió perfil y hay partida (volviendo del juego), directo.
            if (ApiManager.Instance.PartidaId.HasValue)
            {
                StartGame();
                return;
            }

            ShowProfiles();
        }

        // ── Paso 2: perfil de menor ────────────────────────────────────────────
        private void ShowProfiles()
        {
            if (panel        != null) panel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(true);
            if (savePanel    != null) savePanel.SetActive(false);

            SetProfileBusy(true);
            SetProfileStatus("Cargando perfiles…", false);

            ApiManager.Instance.ListarJugadores(
                onSuccess: RenderProfiles,
                onError: err =>
                {
                    SetProfileBusy(false);
                    ClearProfileList();
                    SetProfileStatus(FriendlyError(err), true);
                });
        }

        private void RenderProfiles(List<UsuarioJugadorDto> jugadores)
        {
            SetProfileBusy(false);
            ClearProfileList();

            if (jugadores == null || jugadores.Count == 0)
            {
                SetProfileStatus("Todavía no hay perfiles. Crea el primero aquí abajo.", false);
                return;
            }

            SetProfileStatus("Toca un perfil para jugar.", false);

            foreach (var jugador in jugadores)
            {
                var actual = jugador;   // copia local: si no, todos los botones usarían el último
                string etiqueta = actual.edad.HasValue
                    ? $"{actual.nombre}  ·  {actual.edad} años"
                    : actual.nombre;

                var boton = CreateButton(profileListContainer, etiqueta,
                    new Color(0.16f, 0.4f, 0.55f, 1f), out _, 80f);
                boton.onClick.AddListener(() => ChooseProfile(actual));
            }
        }

        /// <summary>
        /// Paso 2 → paso 3. Antes esto retomaba a ciegas la última partida del menor;
        /// ahora pregunta cuál continuar, que es lo que pide HDU-15.
        ///
        /// <paramref name="preguntar"/> en false conserva el comportamiento viejo
        /// (retomar la más reciente sin mostrar nada). Lo usa el auto-login de pruebas.
        /// </summary>
        private void ChooseProfile(UsuarioJugadorDto jugador, bool preguntar = true)
        {
            if (profileBusy) return;
            perfilElegido = jugador;

            SetProfileBusy(true);
            SetProfileStatus($"Buscando las partidas de {jugador.nombre}…", false);

            // Fijar el perfil ANTES de pedir sus partidas: ApiManager descarta el estado
            // de sesión al cambiar de menor, y hacerlo después borraría la partida que
            // acabamos de adoptar.
            ApiManager.Instance.SeleccionarJugador(jugador.id);

            ApiManager.Instance.ObtenerPartidasJugador(jugador.id,
                onSuccess: partidas =>
                {
                    // Ojo: el perfil sigue marcado como ocupado a propósito en las dos
                    // ramas que entran al juego sin mostrar nada. Liberarlo aquí dejaba
                    // los botones de perfil vivos durante el alta de la partida, y un
                    // segundo toque creaba una segunda partida. Se libera solo cuando
                    // hay una pantalla que el menor pueda tocar, o al fallar.
                    //
                    // Sin partidas guardadas NO se ofrece continuar: se empieza una nueva
                    // y el menor entra directo. Enseñarle una lista vacía con un botón
                    // "Continuar" muerto sería peor que no mostrar la pantalla.
                    if (partidas == null || partidas.Count == 0) { EmpezarPartidaNueva(); return; }

                    if (!preguntar) { ContinuarPartida(partidas[0]); return; }

                    SetProfileBusy(false);
                    ShowSaves(partidas);
                },
                onError: err =>
                {
                    SetProfileBusy(false);
                    SetProfileStatus(FriendlyError(err), true);
                });
        }

        public void CreateProfile()
        {
            if (profileBusy) return;

            string nombre = newProfileNameField != null ? newProfileNameField.text.Trim() : "";
            if (string.IsNullOrEmpty(nombre))
            {
                SetProfileStatus("Escribe el nombre del perfil.", true);
                return;
            }

            int? edad = null;
            string edadTexto = newProfileAgeField != null ? newProfileAgeField.text.Trim() : "";
            if (!string.IsNullOrEmpty(edadTexto))
            {
                if (!int.TryParse(edadTexto, out int parsed) || parsed < 1 || parsed > 120)
                {
                    SetProfileStatus("La edad debe ser un número entre 1 y 120.", true);
                    return;
                }
                edad = parsed;
            }

            SetProfileBusy(true);
            SetProfileStatus("Creando perfil…", false);

            ApiManager.Instance.CrearJugador(nombre, edad,
                onSuccess: _ =>
                {
                    if (newProfileNameField != null) newProfileNameField.text = "";
                    if (newProfileAgeField  != null) newProfileAgeField.text  = "";
                    ShowProfiles();   // recarga la lista para que aparezca el nuevo
                },
                onError: err =>
                {
                    SetProfileBusy(false);
                    SetProfileStatus(FriendlyError(err), true);
                });
        }

        private void ClearProfileList()
        {
            if (profileListContainer == null) return;
            // Se desemparenta antes de destruir: Destroy es diferido hasta el final
            // del frame y si no, el layout seguiría contando los botones viejos.
            for (int i = profileListContainer.childCount - 1; i >= 0; i--)
            {
                var child = profileListContainer.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        // ── Paso 3: continuar partida ──────────────────────────────────────────
        //
        // Solo se llega aquí si el perfil YA tiene al menos una partida guardada.
        // Con cero partidas, ChooseProfile crea una y entra directo: ese es el
        // "ocultar Continuar cuando no hay nada que continuar".

        private void ShowSaves(List<PartidaDto> partidas)
        {
            if (panel        != null) panel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(false);
            if (savePanel    != null) savePanel.SetActive(true);

            SetSaveBusy(false);
            ClearSaveList();

            string nombre = perfilElegido != null ? perfilElegido.nombre : "tu perfil";
            int cuantas = Mathf.Min(partidas.Count, Mathf.Max(1, maxPartidasEnLista));

            SetSaveStatus(partidas.Count > cuantas
                ? $"Partidas de {nombre} (las {cuantas} más recientes)."
                : $"Partidas de {nombre}.", false);

            for (int i = 0; i < cuantas; i++)
            {
                var partida = partidas[i];        // copia local: sin ella todos los
                bool masReciente = i == 0;        // botones usarían la última del bucle

                var boton = CreateButton(saveListContainer, $"Partida {partida.id}",
                    masReciente ? new Color(0.16f, 0.5f, 0.34f, 1f)
                                : new Color(0.16f, 0.4f, 0.55f, 1f),
                    out var etiqueta, 100f);
                etiqueta.fontSize        = 26;
                etiqueta.verticalOverflow = VerticalWrapMode.Overflow;
                etiqueta.text            = TextoDePartida.Etiqueta(partida, masReciente);

                boton.onClick.AddListener(() => ContinuarPartida(partida));
            }
        }

        /// <summary>Entra al juego con una partida ya existente.</summary>
        private void ContinuarPartida(PartidaDto partida)
        {
            if (saveBusy) return;

            SetSaveBusy(true);
            SetSaveStatus("Cargando tu partida…", false);

            if (!ApiManager.Instance.RetomarPartida(partida))
            {
                const string aviso = "Esa partida no se pudo abrir. Prueba con otra.";
                SetSaveBusy(false);
                SetSaveStatus(aviso, true);
                // Se puede llegar aquí sin haber mostrado el panel (auto-login), y ahí
                // el único cartel a la vista es el del paso 2.
                SetProfileBusy(false);
                SetProfileStatus(aviso, true);
                return;
            }

            // Ata misiones e inventario a esta partida antes de entrar. El bucle de
            // MisionBackendSync haría lo mismo en el siguiente tic, pero la escena
            // arranca antes que eso.
            MisionBackendSync.AtarProgresoALaPartida(partida.id);

            Debug.Log($"[AuthScreen] Retomando partida {partida.id} " +
                      $"(progreso {partida.progreso}, guardada {partida.fecha_update}).");
            StartGame();
        }

        /// <summary>Empieza de cero. Lo llama el botón "Empezar una partida nueva" y
        /// también ChooseProfile cuando el perfil todavía no tiene ninguna.</summary>
        private void EmpezarPartidaNueva()
        {
            if (saveBusy) return;
            if (perfilElegido == null)
            {
                SetSaveStatus("Primero elige quién va a jugar.", true);
                ShowProfiles();
                return;
            }

            SetSaveBusy(true);
            SetSaveStatus("Creando una partida nueva…", false);
            // El paso 2 sigue a la vista cuando se llega aquí por no haber ninguna
            // partida guardada; el paso 3, cuando se pulsó "Empezar una partida nueva".
            // El mismo texto sirve en los dos casos.
            SetProfileStatus($"Preparando la partida de {perfilElegido.nombre}…", false);

            ApiManager.Instance.CrearPartida(perfilElegido.id, 0f, null,
                onSuccess: partida =>
                {
                    MisionBackendSync.AtarProgresoALaPartida(partida.id);
                    Debug.Log($"[AuthScreen] Partida {partida.id} creada para {perfilElegido.nombre}.");
                    StartGame();
                },
                onError: err =>
                {
                    SetSaveBusy(false);
                    SetSaveStatus(FriendlyError(err), true);
                    SetProfileBusy(false);
                    SetProfileStatus(FriendlyError(err), true);
                });
        }

        private void ClearSaveList()
        {
            if (saveListContainer == null) return;
            // Se desemparenta antes de destruir: Destroy es diferido hasta el final del
            // frame y si no, el layout seguiría contando los botones viejos.
            for (int i = saveListContainer.childCount - 1; i >= 0; i--)
            {
                var child = saveListContainer.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private void StartGame()
        {
            SetStatus("¡Listo!", false);
            SetProfileStatus("¡Listo!", false);
            SetSaveStatus("¡Listo!", false);
            Hide();
            if (loadingScreen != null) loadingScreen.LoadScene(gameSceneName);
            else SceneManager.LoadScene(gameSceneName);
        }

        // ── Estado de UI ───────────────────────────────────────────────────────
        private void SetBusy(bool value)
        {
            busy = value;
            if (submitButton     != null) submitButton.interactable     = !value;
            if (toggleModeButton != null) toggleModeButton.interactable = !value;
            if (usernameField    != null) usernameField.interactable    = !value;
            if (emailField       != null) emailField.interactable       = !value;
            if (passwordField    != null) passwordField.interactable    = !value;
        }

        private void SetProfileBusy(bool value)
        {
            profileBusy = value;
            if (createProfileButton  != null) createProfileButton.interactable  = !value;
            if (newProfileNameField  != null) newProfileNameField.interactable  = !value;
            if (newProfileAgeField   != null) newProfileAgeField.interactable   = !value;
            if (profileListContainer == null) return;
            foreach (var boton in profileListContainer.GetComponentsInChildren<Button>())
                boton.interactable = !value;
        }

        private void SetSaveBusy(bool value)
        {
            saveBusy = value;
            if (newGameButton        != null) newGameButton.interactable        = !value;
            if (backToProfilesButton != null) backToProfilesButton.interactable = !value;
            if (saveListContainer    == null) return;
            foreach (var boton in saveListContainer.GetComponentsInChildren<Button>())
                boton.interactable = !value;
        }

        private void SetStatus(string message, bool isError) => Paint(statusLabel, message, isError);
        private void SetProfileStatus(string message, bool isError) => Paint(profileStatusLabel, message, isError);
        private void SetSaveStatus(string message, bool isError) => Paint(saveStatusLabel, message, isError);

        private static void Paint(Text label, string message, bool isError)
        {
            if (label == null) return;
            label.text  = message;
            label.color = isError
                ? new Color(1f, 0.45f, 0.45f)
                : new Color(0.65f, 0.82f, 1f);
        }

        private void UpdateConnectionBadge(bool connected)
        {
            if (connectionBadge == null) return;
            connectionBadge.text  = connected ? "🟢 Conectado"         : "🔴 Sin conexión (modo local)";
            connectionBadge.color = connected ? new Color(0.4f, 0.9f, 0.5f) : new Color(1f, 0.6f, 0.3f);
        }

        private void EnsureApiManager()
        {
            if (ApiManager.Instance == null)
            {
                var go = new GameObject("ApiManager");
                go.AddComponent<ApiManager>();  // DontDestroyOnLoad interno
            }
        }

        // ── Mensajes de error amigables ────────────────────────────────────────
        private string FriendlyError(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "No se pudo conectar. Inténtalo de nuevo.";
            string low = raw.ToLowerInvariant();

            // Lo específico primero: el backend devuelve el error del campo que falló
            // y "ya existe" a secas sería ambiguo entre nombre, email y perfil.
            if (low.Contains("valid email") || low.Contains("email válido") ||
                low.Contains("email valido"))
                return "Ese email no parece válido.";
            if (low.Contains("email") && (low.Contains("already") || low.Contains("exist") ||
                                          low.Contains("unique")  || low.Contains("ya existe")))
                return "Ese email ya está registrado.";
            if (low.Contains("perfil con ese nombre"))
                return "Ya tienes un perfil con ese nombre.";
            if (low.Contains("already") || low.Contains("ya existe") ||
                low.Contains("unique")  || low.Contains("exist"))
                return "Ese nombre de usuario ya está en uso.";
            if (low.Contains("invalid") || low.Contains("incorrect") ||
                low.Contains("credential") || low.Contains("contrase") ||
                low.Contains("unable to log"))
                return "Usuario o contraseña incorrectos.";
            if (low.Contains("cannot connect") || low.Contains("timeout") ||
                low.Contains("curl")   || low.Contains("connection") ||
                low.Contains("network") || low.Contains("unreachable"))
                return "No se pudo conectar con el servidor.";
            if (low.Contains("min_length") || low.Contains("short") ||
                low.Contains("4 character"))
                return "La contraseña debe tener al menos 4 caracteres.";
            return "Ocurrió un problema. Inténtalo de nuevo.";
        }

        // ── Construcción de UI en runtime ──────────────────────────────────────
        private void BuildRuntimeUI()
        {
            // Canvas
            var canvasGO = new GameObject("AuthCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1500;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            BuildAuthPanel(canvasGO.transform);
            BuildProfilePanel(canvasGO.transform);
            BuildSavePanel(canvasGO.transform);
        }

        private void BuildAuthPanel(Transform parent)
        {
            // Fondo
            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 1f);

            // Badge de conexión (esquina superior derecha)
            var badgeGO = new GameObject("ConnectionBadge",
                typeof(RectTransform), typeof(Text));
            badgeGO.transform.SetParent(panel.transform, false);
            var badgeRT = badgeGO.GetComponent<RectTransform>();
            badgeRT.anchorMin       = new Vector2(1f, 1f);
            badgeRT.anchorMax       = new Vector2(1f, 1f);
            badgeRT.pivot           = new Vector2(1f, 1f);
            badgeRT.anchoredPosition = new Vector2(-24f, -18f);
            badgeRT.sizeDelta       = new Vector2(480f, 48f);
            connectionBadge = badgeGO.GetComponent<Text>();
            connectionBadge.font      = font;
            connectionBadge.fontSize  = 26;
            connectionBadge.alignment = TextAnchor.MiddleRight;
            connectionBadge.color     = new Color(0.5f, 0.5f, 0.5f);
            connectionBadge.text      = "Conectando…";

            var card = CreateCard(panel.transform, 760f, 800f);

            // Título
            titleLabel = CreateLabel(card, "Iniciar sesión",
                48, FontStyle.Bold, TextAnchor.MiddleCenter, 80f);

            // Campos
            usernameField = CreateInputField(card, "Nombre de la cuenta", password: false);
            emailField    = CreateInputField(card, "Email",               password: false);
            passwordField = CreateInputField(card, "Contraseña",          password: true);
            emailField.contentType = InputField.ContentType.EmailAddress;

            // Botón principal
            submitButton = CreateButton(card, "Entrar",
                new Color(0.16f, 0.5f, 0.34f, 1f), out submitLabel, 86f);

            // Estado / error
            statusLabel = CreateLabel(card, "",
                26, FontStyle.Normal, TextAnchor.MiddleCenter, 60f);
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow   = VerticalWrapMode.Overflow;

            // Cambiar modo
            toggleModeButton = CreateButton(card,
                "¿Primera vez? Crear cuenta",
                new Color(0.16f, 0.22f, 0.34f, 1f), out toggleModeLabel, 70f);
        }

        private void BuildProfilePanel(Transform parent)
        {
            profilePanel = new GameObject("ProfilePanel", typeof(RectTransform), typeof(Image));
            profilePanel.transform.SetParent(parent, false);
            Stretch(profilePanel.GetComponent<RectTransform>());
            profilePanel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 1f);
            profilePanel.SetActive(false);

            var card = CreateCard(profilePanel.transform, 860f, 900f);

            CreateLabel(card, "¿Quién va a jugar?",
                48, FontStyle.Bold, TextAnchor.MiddleCenter, 80f);

            var subtitulo = CreateLabel(card,
                "Cada perfil guarda su propio avance.",
                26, FontStyle.Italic, TextAnchor.MiddleCenter, 44f);
            subtitulo.color = new Color(0.6f, 0.68f, 0.78f);

            // Contenedor de la lista de perfiles (se rellena en runtime)
            var listGO = new GameObject("ProfileList",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            listGO.transform.SetParent(card, false);
            var listVlg = listGO.GetComponent<VerticalLayoutGroup>();
            listVlg.spacing                = 14f;
            listVlg.childAlignment         = TextAnchor.UpperCenter;
            listVlg.childControlWidth      = true;
            listVlg.childControlHeight     = true;
            listVlg.childForceExpandWidth  = true;
            listVlg.childForceExpandHeight = false;
            listGO.GetComponent<LayoutElement>().minHeight = 180f;
            profileListContainer = listGO.transform;

            profileStatusLabel = CreateLabel(card, "",
                26, FontStyle.Normal, TextAnchor.MiddleCenter, 60f);
            profileStatusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            profileStatusLabel.verticalOverflow   = VerticalWrapMode.Overflow;

            var separador = CreateLabel(card, "— o crea uno nuevo —",
                24, FontStyle.Normal, TextAnchor.MiddleCenter, 44f);
            separador.color = new Color(0.45f, 0.52f, 0.62f);

            newProfileNameField = CreateInputField(card, "Nombre del perfil", password: false);
            newProfileAgeField  = CreateInputField(card, "Edad (opcional)",   password: false);
            newProfileAgeField.contentType   = InputField.ContentType.IntegerNumber;
            newProfileAgeField.characterLimit = 3;

            createProfileButton = CreateButton(card, "Agregar perfil",
                new Color(0.16f, 0.4f, 0.55f, 1f), out _, 78f);
        }

        private void BuildSavePanel(Transform parent)
        {
            savePanel = new GameObject("SavePanel", typeof(RectTransform), typeof(Image));
            savePanel.transform.SetParent(parent, false);
            Stretch(savePanel.GetComponent<RectTransform>());
            savePanel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 1f);
            savePanel.SetActive(false);

            var card = CreateCard(savePanel.transform, 900f, 920f);

            CreateLabel(card, "¿Seguimos tu aventura?",
                48, FontStyle.Bold, TextAnchor.MiddleCenter, 80f);

            var subtitulo = CreateLabel(card,
                "Elige una partida para continuar donde quedaste.",
                26, FontStyle.Italic, TextAnchor.MiddleCenter, 44f);
            subtitulo.color = new Color(0.6f, 0.68f, 0.78f);

            // Contenedor de la lista de partidas (se rellena en runtime)
            var listGO = new GameObject("SaveList",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            listGO.transform.SetParent(card, false);
            var listVlg = listGO.GetComponent<VerticalLayoutGroup>();
            listVlg.spacing                = 14f;
            listVlg.childAlignment         = TextAnchor.UpperCenter;
            listVlg.childControlWidth      = true;
            listVlg.childControlHeight     = true;
            listVlg.childForceExpandWidth  = true;
            listVlg.childForceExpandHeight = false;
            listGO.GetComponent<LayoutElement>().minHeight = 110f;
            saveListContainer = listGO.transform;

            saveStatusLabel = CreateLabel(card, "",
                26, FontStyle.Normal, TextAnchor.MiddleCenter, 60f);
            saveStatusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            saveStatusLabel.verticalOverflow   = VerticalWrapMode.Overflow;

            var separador = CreateLabel(card, "— o empieza de nuevo —",
                24, FontStyle.Normal, TextAnchor.MiddleCenter, 44f);
            separador.color = new Color(0.45f, 0.52f, 0.62f);

            newGameButton = CreateButton(card, "Empezar una partida nueva",
                new Color(0.16f, 0.22f, 0.34f, 1f), out _, 78f);

            backToProfilesButton = CreateButton(card, "Cambiar de perfil",
                new Color(0.14f, 0.17f, 0.24f, 1f), out _, 66f);
        }

        // ── Helpers de construcción ────────────────────────────────────────────
        private Transform CreateCard(Transform parent, float width, float height)
        {
            var card = new GameObject("Card",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(parent, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin  = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax  = new Vector2(0.5f, 0.5f);
            cardRT.pivot      = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta  = new Vector2(width, height);
            card.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.2f, 1f);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding             = new RectOffset(50, 50, 40, 40);
            vlg.spacing             = 22f;
            vlg.childAlignment      = TextAnchor.UpperCenter;
            vlg.childControlWidth   = true;
            vlg.childControlHeight  = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            return card.transform;
        }

        private InputField CreateInputField(Transform parent, string placeholder, bool password)
        {
            var go = new GameObject(placeholder,
                typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.93f, 0.95f, 0.98f, 1f);
            go.GetComponent<LayoutElement>().minHeight = 84f;

            var input = go.GetComponent<InputField>();

            // Texto principal
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            Stretch(textGO.GetComponent<RectTransform>(), 20f, 10f);
            var text = textGO.GetComponent<Text>();
            text.font      = font;
            text.fontSize  = 32;
            text.color     = new Color(0.1f, 0.12f, 0.16f);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            // Placeholder
            var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGO.transform.SetParent(go.transform, false);
            Stretch(phGO.GetComponent<RectTransform>(), 20f, 10f);
            var ph = phGO.GetComponent<Text>();
            ph.font      = font;
            ph.fontSize  = 32;
            ph.fontStyle = FontStyle.Italic;
            ph.color     = new Color(0.45f, 0.5f, 0.55f);
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text      = placeholder;

            input.textComponent = text;
            input.placeholder   = ph;
            input.lineType      = InputField.LineType.SingleLine;
            input.contentType   = password
                ? InputField.ContentType.Password
                : InputField.ContentType.Standard;
            input.characterLimit = 64;

            return input;
        }

        private Button CreateButton(Transform parent, string label,
            Color color, out Text labelOut, float minHeight)
        {
            var go = new GameObject(label,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<LayoutElement>().minHeight = minHeight;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(go.transform, false);
            Stretch(txtGO.GetComponent<RectTransform>(), 10f, 4f);
            labelOut = txtGO.GetComponent<Text>();
            labelOut.font      = font;
            labelOut.fontSize  = 30;
            labelOut.color     = Color.white;
            labelOut.alignment = TextAnchor.MiddleCenter;
            labelOut.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelOut.text = label;

            return go.GetComponent<Button>();
        }

        private Text CreateLabel(Transform parent, string text,
            int size, FontStyle style, TextAnchor anchor, float minHeight)
        {
            var go = new GameObject("Label",
                typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().minHeight = minHeight;
            var t = go.GetComponent<Text>();
            t.font      = font;
            t.fontSize  = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color     = Color.white;
            t.text      = text;
            return t;
        }

        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = new Vector2(padX, padY);
            rt.offsetMax  = new Vector2(-padX, -padY);
        }
    }
}
