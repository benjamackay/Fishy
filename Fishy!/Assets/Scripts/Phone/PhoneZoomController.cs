using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Fishy.World;

namespace Fishy.Phone
{
    /// <summary>
    /// Gestiona la transición diegética: la cámara hace zoom hacia el celular de Otto,
    /// funde a negro, muestra la pantalla del chat y al cerrar revierte la cámara.
    ///
    /// Crea el overlay de fade automáticamente. Pausa/reanuda <see cref="CameraFollow2D"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhoneZoomController : MonoBehaviour
    {
        public static PhoneZoomController Instance { get; private set; }

        [Header("Zoom")]
        [Tooltip("Segundos que dura el zoom hacia el celular.")]
        public float zoomInDuration  = 0.65f;
        [Tooltip("Segundos que dura el zoom de vuelta al mundo.")]
        public float zoomOutDuration = 0.45f;
        [Tooltip("Tamaño ortográfico al llegar al celular (más pequeño = más cerca).")]
        public float phoneOrthoSize  = 0.9f;
        public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Fade")]
        [Tooltip("Duración del fundido a negro entre el zoom y la UI del celular.")]
        public float fadeDuration = 0.22f;

        // Estado
        public bool IsInPhoneMode { get; private set; }

        // Cámara
        private Camera         _cam;
        private CameraFollow2D _follow;
        private float          _savedOrtho;
        private Vector3        _savedCamPos;

        // Overlay de fade
        private CanvasGroup _fadeGroup;

        // ── Unity ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _cam    = Camera.main;
            _follow = FindAnyObjectByType<CameraFollow2D>();
            if (_cam != null) _savedOrtho = _cam.orthographicSize;

            BuildFadeOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── API ────────────────────────────────────────────────────────────────
        /// <summary>
        /// Zoom hacia el celular; invoca <paramref name="onArrived"/> cuando la pantalla
        /// ya está en negro y lista para mostrar la UI de chat.
        /// </summary>
        public void ZoomToPhone(OttoPhone phone, Action onArrived)
        {
            if (IsInPhoneMode) { onArrived?.Invoke(); return; }
            StopAllCoroutines();
            StartCoroutine(ZoomInRoutine(
                phone != null ? phone.transform.position : Vector3.zero,
                onArrived));
        }

        /// <summary>Oculta la UI de chat y hace zoom de regreso al mundo.</summary>
        public void ZoomOut(Action onComplete = null)
        {
            if (!IsInPhoneMode) { onComplete?.Invoke(); return; }
            StopAllCoroutines();
            StartCoroutine(ZoomOutRoutine(onComplete));
        }

        // ── Rutinas ────────────────────────────────────────────────────────────
        private IEnumerator ZoomInRoutine(Vector3 phoneWorldPos, Action onArrived)
        {
            IsInPhoneMode = true;

            // Guardar estado de cámara y pausar follow.
            if (_cam != null)
            {
                _savedOrtho  = _cam.orthographicSize;
                _savedCamPos = _cam.transform.position;
            }
            if (_follow != null) _follow.enabled = false;

            // Zoom suave hacia el celular.
            float  startOrtho = _cam != null ? _cam.orthographicSize : 5f;
            Vector3 startPos  = _cam != null ? _cam.transform.position : Vector3.back * 10f;
            Vector3 targetPos = new Vector3(phoneWorldPos.x, phoneWorldPos.y,
                                    _cam != null ? _cam.transform.position.z : -10f);

            float elapsed = 0f;
            while (elapsed < zoomInDuration)
            {
                elapsed += Time.deltaTime;
                float t = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomInDuration));
                if (_cam != null)
                {
                    _cam.orthographicSize     = Mathf.Lerp(startOrtho, phoneOrthoSize, t);
                    _cam.transform.position   = Vector3.Lerp(startPos,  targetPos, t);
                }
                yield return null;
            }

            // Fade a negro: cubre el corte entre el mundo y la UI del celular.
            yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

            // Notificar al launcher; él abrirá el chat mientras la pantalla está negra.
            onArrived?.Invoke();

            // Esperar un frame para que la UI del chat termine de inicializarse.
            yield return null;

            // Revelar la pantalla del celular fundiendo de negro a transparente.
            yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));
        }

        private IEnumerator ZoomOutRoutine(Action onComplete)
        {
            // Fade a negro para cubrir la transición de la UI al mundo.
            // (El fade está en 0 tras el ZoomIn; si por alguna razón ya está
            // en negro, el if evita un parpadeo innecesario.)
            if (_fadeGroup != null && _fadeGroup.alpha < 0.9f)
                yield return StartCoroutine(FadeRoutine(_fadeGroup.alpha, 1f, fadeDuration));

            IsInPhoneMode = false;

            // Restaurar cámara (el negro cubre el corte).
            if (_cam != null)
            {
                _cam.orthographicSize   = _savedOrtho;
                _cam.transform.position = _savedCamPos;
            }
            if (_follow != null)
            {
                _follow.enabled = true;
                _follow.SnapToTarget();
            }

            // Fade de vuelta a transparente.
            yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));

            onComplete?.Invoke();
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (_fadeGroup == null) yield break;
            _fadeGroup.gameObject.SetActive(true);
            float elapsed = 0f;
            _fadeGroup.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _fadeGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _fadeGroup.alpha = to;
            if (to <= 0f) _fadeGroup.gameObject.SetActive(false);
        }

        // ── Overlay ────────────────────────────────────────────────────────────
        private void BuildFadeOverlay()
        {
            Fishy.UI.UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("PhoneFadeCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasGO);
            canvasGO.transform.SetParent(transform, false);

            var cv = canvasGO.GetComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 9000; // por encima de todo

            var overlayGO = new GameObject("FadeOverlay",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var rt = overlayGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            overlayGO.GetComponent<Image>().color = Color.black;

            _fadeGroup = overlayGO.GetComponent<CanvasGroup>();
            _fadeGroup.alpha           = 0f;
            _fadeGroup.blocksRaycasts  = false;
            overlayGO.SetActive(false);
        }
    }
}
