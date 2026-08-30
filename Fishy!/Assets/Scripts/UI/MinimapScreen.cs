using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Fishy.World;

namespace Fishy.UI
{
    /// <summary>
    /// Mapa de la zona: se abre/cierra con una tecla (por defecto M o Tab) y
    /// muestra dónde está parado el jugador con un punto rojo.
    ///
    /// La UI es deliberadamente mínima: un recuadro que hace de mapa y el punto
    /// del jugador encima. La idea es reemplazar el fondo por la imagen real del
    /// mapa asignando <see cref="mapImage"/> en el Inspector — el punto rojo se
    /// seguirá posicionando solo, porque su posición se calcula a partir de los
    /// límites del mundo, no de la imagen.
    ///
    /// Cómo se calculan los límites del mundo (en este orden):
    ///   1. <see cref="usarLimitesManuales"/> → minMundo / maxMundo.
    ///   2. <see cref="limitesCollider"/> si se asigna un Collider2D.
    ///   3. El <see cref="CameraFollow2D"/> de la escena, si tiene límites activos.
    ///   4. Automático: la extensión combinada de los Tilemaps de la escena.
    ///
    /// IMPORTANTE: para que el punto caiga donde corresponde, la imagen del mapa
    /// debe estar dibujada con la misma proporción que la zona jugable. Con
    /// <see cref="ajustarPanelAlAspectoDelMundo"/> activo el recuadro se
    /// redimensiona solo a esa proporción.
    /// </summary>
    public class MinimapScreen : MonoBehaviour
    {
        [Header("Jugador")]
        [Tooltip("Transform del jugador (Otto). Si se deja vacío se busca por tag.")]
        public Transform player;
        [Tooltip("Tag con el que se busca al jugador si no se asignó arriba.")]
        public string playerTag = "Player";

        [Header("Apariencia del mapa")]
        [Tooltip("Imagen del mapa de la zona. Si se deja vacía se usa un recuadro liso de relleno.")]
        public Sprite mapImage;
        [Tooltip("Tamaño máximo del mapa en pantalla (px de la resolución de referencia 1920x1080).")]
        public Vector2 tamanoMaximo = new Vector2(1200f, 800f);
        [Tooltip("Redimensiona el recuadro para que tenga la misma proporción que la zona jugable.")]
        public bool ajustarPanelAlAspectoDelMundo = true;

        [Header("Punto del jugador")]
        public Color colorPunto = Color.red;
        [Tooltip("Diámetro del punto en píxeles.")]
        public float tamanoPunto = 24f;
        [Tooltip("Parpadeo suave del punto para que se note. 0 = sin parpadeo.")]
        public float pulsoPunto = 0.15f;

        [Header("Límites del mundo")]
        [Tooltip("Usar los límites escritos abajo en vez de detectarlos solo.")]
        public bool usarLimitesManuales = false;
        public Vector2 minMundo = new Vector2(-48f, -94f);
        public Vector2 maxMundo = new Vector2(168f, 25f);
        [Tooltip("Si se asigna, sus bounds definen la zona jugable (tiene prioridad sobre la detección automática).")]
        public Collider2D limitesCollider;

        [Header("Comportamiento")]
        [Tooltip("Bloquear el movimiento del jugador mientras el mapa está abierto.")]
        public bool bloquearMovimiento = true;
        [Tooltip("Teclas que abren/cierran el mapa (rutas del nuevo Input System).")]
        public string[] teclasMapa = { "<Keyboard>/m", "<Keyboard>/tab" };

        [Header("Referencias (opcionales; se generan en runtime)")]
        public GameObject panel;
        public Image mapPanel;
        public Image playerDot;

        private RectTransform mapRect;
        private RectTransform dotRect;
        private InputAction toggleAction;
        private InputAction closeAction;
        private OttoController otto;
        private bool movimientoEstabaActivo;
        private Vector2 boundsMin, boundsMax;
        private bool boundsListos;

        /// <summary>True si el mapa está visible en este momento.</summary>
        public bool IsOpen => panel != null && panel.activeSelf;

        // ── Ciclo de vida ──────────────────────────────────────────────────────
        private void Awake()
        {
            UiBootstrap.EnsureEventSystem();

            if (panel == null) BuildRuntimeUI();

            BuildInputActions();
            Close();
        }

        private void Start()
        {
            ResolvePlayer();
            ResolveWorldBounds();
            ApplyMapAspect();
        }

        private void BuildInputActions()
        {
            toggleAction = new InputAction("ToggleMapa", InputActionType.Button);
            if (teclasMapa != null)
            {
                foreach (string binding in teclasMapa)
                    if (!string.IsNullOrEmpty(binding))
                        toggleAction.AddBinding(binding);
            }
            toggleAction.AddBinding("<Gamepad>/select");

            // Escape solo cierra (nunca abre), para no chocar con menús de pausa.
            closeAction = new InputAction("CerrarMapa", InputActionType.Button);
            closeAction.AddBinding("<Keyboard>/escape");
        }

        private void OnEnable()
        {
            toggleAction?.Enable();
            closeAction?.Enable();
        }

        private void OnDisable()
        {
            toggleAction?.Disable();
            closeAction?.Disable();
        }

        private void OnDestroy()
        {
            toggleAction?.Dispose();
            closeAction?.Dispose();
        }

        private void Update()
        {
            if (toggleAction != null && toggleAction.WasPressedThisFrame())
                Toggle();
            else if (IsOpen && closeAction != null && closeAction.WasPressedThisFrame())
                Close();

            if (IsOpen) UpdatePlayerDot();
        }

        // ── Abrir / cerrar ─────────────────────────────────────────────────────
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (panel == null) return;

            ResolvePlayer();
            if (!boundsListos) ResolveWorldBounds();
            ApplyMapAspect();

            panel.SetActive(true);
            UpdatePlayerDot();

            if (bloquearMovimiento && otto != null)
            {
                // Recordamos el estado previo: si el movimiento ya estaba bloqueado
                // (diálogo, cinemática...), al cerrar el mapa NO debemos reactivarlo.
                movimientoEstabaActivo = otto.movementEnabled;
                otto.DisableMovement();
            }
        }

        public void Close()
        {
            if (panel == null) return;
            panel.SetActive(false);

            if (bloquearMovimiento && otto != null && movimientoEstabaActivo)
                otto.EnableMovement();
        }

        // ── Posición del jugador en el mapa ────────────────────────────────────
        private void UpdatePlayerDot()
        {
            if (dotRect == null) return;

            if (player == null)
            {
                ResolvePlayer();
                if (player == null) { dotRect.gameObject.SetActive(false); return; }
            }
            dotRect.gameObject.SetActive(true);

            Vector2 n = WorldToNormalized(player.position);

            // Anclar el punto en coordenadas normalizadas hace que siga correcto
            // aunque el mapa cambie de tamaño o de resolución.
            dotRect.anchorMin = n;
            dotRect.anchorMax = n;
            dotRect.anchoredPosition = Vector2.zero;

            if (pulsoPunto > 0f && playerDot != null)
            {
                float t = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
                float escala = 1f + pulsoPunto * t;
                dotRect.localScale = new Vector3(escala, escala, 1f);
            }
        }

        /// <summary>
        /// Convierte una posición del mundo a coordenadas 0..1 dentro de la zona
        /// jugable (0,0 = esquina inferior izquierda; 1,1 = superior derecha).
        /// </summary>
        public Vector2 WorldToNormalized(Vector3 worldPos)
        {
            float ancho = boundsMax.x - boundsMin.x;
            float alto  = boundsMax.y - boundsMin.y;
            if (ancho <= 0.0001f || alto <= 0.0001f) return new Vector2(0.5f, 0.5f);

            return new Vector2(
                Mathf.Clamp01((worldPos.x - boundsMin.x) / ancho),
                Mathf.Clamp01((worldPos.y - boundsMin.y) / alto));
        }

        // ── Resolución de referencias / límites ────────────────────────────────
        private void ResolvePlayer()
        {
            if (player == null && !string.IsNullOrEmpty(playerTag))
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go != null) player = go.transform;
            }
            if (otto == null && player != null)
                otto = player.GetComponent<OttoController>();
        }

        /// <summary>
        /// Calcula los límites de la zona jugable con la primera fuente disponible.
        /// </summary>
        public void ResolveWorldBounds()
        {
            boundsListos = true;

            if (usarLimitesManuales)
            {
                boundsMin = minMundo;
                boundsMax = maxMundo;
                return;
            }

            if (limitesCollider != null)
            {
                boundsMin = limitesCollider.bounds.min;
                boundsMax = limitesCollider.bounds.max;
                return;
            }

            var cam = FindFirstObjectByType<CameraFollow2D>();
            if (cam != null && cam.boundsCollider != null)
            {
                boundsMin = cam.boundsCollider.bounds.min;
                boundsMax = cam.boundsCollider.bounds.max;
                return;
            }
            if (cam != null && cam.useBounds)
            {
                boundsMin = cam.minBounds;
                boundsMax = cam.maxBounds;
                return;
            }

            if (TryGetTilemapBounds(out Bounds tilemapBounds))
            {
                boundsMin = tilemapBounds.min;
                boundsMax = tilemapBounds.max;
                return;
            }

            // Último recurso: los valores escritos en el Inspector.
            boundsMin = minMundo;
            boundsMax = maxMundo;
            Debug.LogWarning("[MinimapScreen] No se pudieron detectar los límites del mundo. " +
                             "Usando minMundo/maxMundo del Inspector.");
        }

        /// <summary>Extensión combinada de todos los Tilemaps dibujados en la escena.</summary>
        private bool TryGetTilemapBounds(out Bounds result)
        {
            result = new Bounds();
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool primero = true;

            foreach (var tm in tilemaps)
            {
                var cells = tm.cellBounds;
                if (cells.size.x <= 0 || cells.size.y <= 0) continue;

                // CellToWorld da la esquina de la celda, así que el máximo se toma
                // en cellBounds.max (que ya es exclusivo) para cubrir la última celda.
                Vector3 min = tm.CellToWorld(cells.min);
                Vector3 max = tm.CellToWorld(cells.max);
                var b = new Bounds();
                b.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));

                if (primero) { result = b; primero = false; }
                else result.Encapsulate(b);
            }

            return !primero;
        }

        /// <summary>Redimensiona el recuadro del mapa a la proporción de la zona jugable.</summary>
        private void ApplyMapAspect()
        {
            if (mapRect == null) return;

            float ancho = boundsMax.x - boundsMin.x;
            float alto  = boundsMax.y - boundsMin.y;

            if (!ajustarPanelAlAspectoDelMundo || ancho <= 0.0001f || alto <= 0.0001f)
            {
                mapRect.sizeDelta = tamanoMaximo;
                return;
            }

            // Encajar el rectángulo del mundo dentro de tamanoMaximo sin deformarlo.
            float escala = Mathf.Min(tamanoMaximo.x / ancho, tamanoMaximo.y / alto);
            mapRect.sizeDelta = new Vector2(ancho * escala, alto * escala);
        }

        // ── Construcción de UI en runtime ──────────────────────────────────────
        private void BuildRuntimeUI()
        {
            var canvasGO = new GameObject("MinimapCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Fondo oscurecido.
            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            // Recuadro del mapa (aquí va la imagen real del mapa).
            var mapGO = new GameObject("Map", typeof(RectTransform), typeof(Image));
            mapGO.transform.SetParent(panel.transform, false);
            mapRect = mapGO.GetComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.pivot     = new Vector2(0.5f, 0.5f);
            mapRect.sizeDelta = tamanoMaximo;
            mapPanel = mapGO.GetComponent<Image>();
            if (mapImage != null)
            {
                mapPanel.sprite = mapImage;
                mapPanel.color  = Color.white;
            }
            else
            {
                mapPanel.color = new Color(0.13f, 0.17f, 0.24f, 1f);
            }

            // Punto del jugador (hijo del mapa → se posiciona por anchors 0..1).
            var dotGO = new GameObject("PlayerDot", typeof(RectTransform), typeof(Image));
            dotGO.transform.SetParent(mapGO.transform, false);
            dotRect = dotGO.GetComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(tamanoPunto, tamanoPunto);
            dotRect.pivot     = new Vector2(0.5f, 0.5f);
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerDot = dotGO.GetComponent<Image>();
            playerDot.sprite = UiTheme.Circle;
            playerDot.color  = colorPunto;

            // Título y pista, mínimos.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(panel.transform, false);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot     = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -40f);
            titleRT.sizeDelta = new Vector2(800f, 60f);
            var title = titleGO.GetComponent<Text>();
            title.font = font;
            title.fontSize = 40;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = UiTheme.TextPrimary;
            title.text = "Mapa";

            var hintGO = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGO.transform.SetParent(panel.transform, false);
            var hintRT = hintGO.GetComponent<RectTransform>();
            hintRT.anchorMin = new Vector2(0.5f, 0f);
            hintRT.anchorMax = new Vector2(0.5f, 0f);
            hintRT.pivot     = new Vector2(0.5f, 0f);
            hintRT.anchoredPosition = new Vector2(0f, 40f);
            hintRT.sizeDelta = new Vector2(900f, 40f);
            var hint = hintGO.GetComponent<Text>();
            hint.font = font;
            hint.fontSize = 24;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = UiTheme.TextMuted;
            hint.text = "Estás en el punto rojo · M para cerrar";
        }

        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
