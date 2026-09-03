using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.World
{
    /// <summary>
    /// HDU-2 / HDU-5 — Secuencia cinematográfica de desbloqueo de zona.
    ///
    /// Cuando el jugador completa los gatillantes de una zona (p.ej. termina de
    /// hablar con los 2 NPCs de "Desconocidos"), la cámara hace una panorámica con
    /// zoom hacia la zona bloqueada (oscurecida), se muestra cómo se "ilumina" al
    /// desbloquearse (el oscurecido se desvanece) junto a un cartel, y luego la
    /// cámara regresa suavemente a Otto.
    ///
    /// No requiere montaje: se crea por código (GetOrCreate) y genera su propio
    /// cartel de UI. Pausa <see cref="CameraFollow2D"/> y el movimiento de Otto
    /// durante la secuencia.
    /// </summary>
    public class ZoneUnlockCinematic : MonoBehaviour
    {
        public static ZoneUnlockCinematic Instance { get; private set; }

        [Header("Ritmo (segundos)")]
        [Tooltip("Paneo + zoom desde Otto hacia la zona.")]
        public float panInDuration  = 1.1f;
        [Tooltip("Desvanecido del oscurecido (la zona se ilumina).")]
        public float revealDuration = 1.0f;
        [Tooltip("Pausa con la zona ya iluminada y el cartel visible.")]
        public float holdDuration   = 0.9f;
        [Tooltip("Regreso de la cámara a Otto.")]
        public float panOutDuration = 0.9f;

        [Header("Encuadre")]
        [Tooltip("Margen al encuadrar la zona (1 = ajustado, 1.3 = con aire alrededor).")]
        public float framePadding = 1.25f;
        [Tooltip("Tamaño ortográfico de respaldo si no se puede medir la zona.")]
        public float fallbackOrthoSize = 4.5f;

        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>True mientras la secuencia se está reproduciendo.</summary>
        public bool IsPlaying { get; private set; }

        private Camera _cam;
        private CameraFollow2D _follow;
        private Text _banner;
        private CanvasGroup _bannerGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public static ZoneUnlockCinematic GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("ZoneUnlockCinematic");
                Instance = go.AddComponent<ZoneUnlockCinematic>();
            }
            return Instance;
        }

        /// <summary>
        /// Reproduce la secuencia: enfoca la zona, la desbloquea con animación y
        /// regresa la cámara a Otto. Llama a <paramref name="onComplete"/> al terminar.
        /// Si no hay cámara/zona, hace el desbloqueo directo (sin cinemática).
        /// </summary>
        public void Play(BlockedZone zone,
            string mensaje = "✨ ¡Nueva zona desbloqueada!", Action onComplete = null)
        {
            _cam = _cam != null ? _cam : Camera.main;

            if (zone == null || _cam == null || IsPlaying)
            {
                if (zone != null) zone.Unlock();   // fallback: desbloqueo inmediato
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(PlayRoutine(zone, mensaje, onComplete));
        }

        // ── Secuencia ────────────────────────────────────────────────────────────
        private IEnumerator PlayRoutine(BlockedZone zone, string mensaje, Action onComplete)
        {
            IsPlaying = true;

            // Bloquear movimiento de Otto y pausar el seguimiento de la cámara.
            var otto = FindAnyObjectByType<OttoController>();
            if (otto != null) otto.DisableMovement();
            if (_follow == null) _follow = FindAnyObjectByType<CameraFollow2D>();
            if (_follow != null) _follow.enabled = false;

            float   startOrtho = _cam.orthographicSize;
            Vector3 startPos   = _cam.transform.position;

            // Encuadre objetivo sobre la zona.
            Vector2 center      = zone.WorldCenter;
            Vector3 targetPos   = new Vector3(center.x, center.y, startPos.z);
            float   targetOrtho = ComputeOrtho(zone, startOrtho);

            // 1) Paneo + zoom hacia la zona oscurecida.
            yield return Tween(startPos, targetPos, startOrtho, targetOrtho, panInDuration);

            // 2) Cartel "¡Zona desbloqueada!".
            ShowBanner(mensaje);

            // 3) Revelar: desvanecer el oscurecido (darkenAlpha -> 0).
            float a0 = zone.CurrentDarkenAlpha;
            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.deltaTime;
                zone.SetOverlayAlpha(Mathf.Lerp(a0, 0f, Mathf.Clamp01(elapsed / revealDuration)));
                yield return null;
            }
            // Desbloqueo efectivo: desactiva los colliders (el oscurecido ya está en 0).
            zone.Unlock();

            // 4) Mantener un momento con la zona iluminada.
            yield return new WaitForSeconds(holdDuration);

            // 5) Ocultar cartel y regresar la cámara a Otto.
            HideBanner();
            Vector3 backPos = otto != null
                ? new Vector3(otto.transform.position.x, otto.transform.position.y, startPos.z)
                : startPos;
            yield return Tween(_cam.transform.position, backPos, _cam.orthographicSize, startOrtho, panOutDuration);

            // Restaurar seguimiento y control.
            if (_follow != null) { _follow.enabled = true; _follow.SnapToTarget(); }
            if (otto != null) otto.EnableMovement();

            IsPlaying = false;
            onComplete?.Invoke();
        }

        /// <summary>Tamaño ortográfico para encuadrar la zona (con margen), acotado.</summary>
        private float ComputeOrtho(BlockedZone zone, float startOrtho)
        {
            Vector2 size = zone.WorldSize;
            if (size == Vector2.zero)
                return fallbackOrthoSize > 0f ? fallbackOrthoSize : startOrtho;

            float aspect      = _cam.aspect > 0.01f ? _cam.aspect : 16f / 9f;
            float halfH       = size.y * 0.5f * framePadding;
            float halfWAsOrtho = (size.x * 0.5f * framePadding) / aspect;
            float ortho       = Mathf.Max(halfH, halfWAsOrtho);

            // Acotar: ni demasiado cerca ni alejarse más de 1.5x el encuadre actual.
            return Mathf.Clamp(ortho, 1.5f, startOrtho * 1.5f);
        }

        private IEnumerator Tween(Vector3 fromPos, Vector3 toPos, float fromOrtho, float toOrtho, float dur)
        {
            if (dur <= 0f)
            {
                _cam.transform.position = toPos;
                _cam.orthographicSize   = toOrtho;
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = ease.Evaluate(Mathf.Clamp01(elapsed / dur));
                _cam.transform.position = Vector3.Lerp(fromPos, toPos, t);
                _cam.orthographicSize   = Mathf.Lerp(fromOrtho, toOrtho, t);
                yield return null;
            }
            _cam.transform.position = toPos;
            _cam.orthographicSize   = toOrtho;
        }

        // ── Cartel ────────────────────────────────────────────────────────────────
        private void ShowBanner(string text)
        {
            EnsureBanner();
            if (_banner != null) _banner.text = text;
            StartCoroutine(FadeBanner(1f));
        }

        private void HideBanner() => StartCoroutine(FadeBanner(0f));

        private void EnsureBanner()
        {
            if (_banner != null) return;
            Fishy.UI.UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("ZoneUnlockCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var cv = canvasGO.GetComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 8000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            var panel = new GameObject("Banner",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasGO.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.82f);
            rt.anchorMax = new Vector2(0.5f, 0.82f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(960f, 130f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.92f);
            _bannerGroup = panel.GetComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(panel.transform, false);
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(24f, 16f); trt.offsetMax = new Vector2(-24f, -16f);
            _banner = txtGO.GetComponent<Text>();
            _banner.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _banner.fontSize  = 44;
            _banner.fontStyle = FontStyle.Bold;
            _banner.alignment = TextAnchor.MiddleCenter;
            _banner.color     = new Color(0.85f, 0.95f, 1f);
            _banner.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private IEnumerator FadeBanner(float to)
        {
            if (_bannerGroup == null) yield break;
            float from = _bannerGroup.alpha;
            float elapsed = 0f, dur = 0.25f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                _bannerGroup.alpha = Mathf.Lerp(from, to, elapsed / dur);
                yield return null;
            }
            _bannerGroup.alpha = to;
        }
    }
}
