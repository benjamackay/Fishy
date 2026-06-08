using System;
using System.Collections;
using UnityEngine;
using Fishy.World;

namespace Fishy.Phone
{
    /// <summary>
    /// El celular diegético de Otto: existe como objeto hijo en el mundo.
    /// Vibra y enciende su pantalla cuando llega un mensaje peligroso.
    ///
    /// Colócalo como hijo de Otto (ej. en su mano / bolsillo).
    /// Si no existe uno en la escena, <see cref="PhoneChatLauncher"/> lo crea automáticamente.
    /// </summary>
    public class OttoPhone : MonoBehaviour
    {
        public static OttoPhone Instance { get; private set; }

        [Header("Vibración")]
        [Tooltip("Desplazamiento máximo de la vibración (unidades locales).")]
        public float vibrateIntensity = 0.04f;
        public float vibrateDuration  = 0.7f;
        [Tooltip("Número de pulsos de vibración.")]
        public int   vibrateCount     = 3;

        [Header("Pantalla (opcional)")]
        [Tooltip("SpriteRenderer de la pantalla del celular. Se ilumina al activarse.")]
        public SpriteRenderer screenRenderer;
        public Color screenOffColor = new Color(0.08f, 0.08f, 0.12f, 1f);
        public Color screenOnColor  = new Color(0.25f, 0.55f, 1.00f, 0.9f);

        private Vector3 _baseLPos;
        private bool    _vibrating;

        // ── Unity ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance   = this;
            _baseLPos  = transform.localPosition;
            if (screenRenderer != null) screenRenderer.color = screenOffColor;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── API pública ────────────────────────────────────────────────────────
        /// <summary>Vibra el celular y enciende la pantalla; llama a <paramref name="onComplete"/> al acabar.</summary>
        public void Vibrate(Action onComplete = null)
        {
            if (_vibrating) { onComplete?.Invoke(); return; }
            StartCoroutine(VibrateRoutine(onComplete));
        }

        public void TurnScreenOn()
        {
            if (screenRenderer != null) screenRenderer.color = screenOnColor;
        }

        public void TurnScreenOff()
        {
            if (screenRenderer != null) screenRenderer.color = screenOffColor;
        }

        // ── Búsqueda ───────────────────────────────────────────────────────────
        /// <summary>
        /// Localiza el OttoPhone que es hijo del <paramref name="otto"/> dado.
        /// Si no hay ninguno, crea uno vacío (sin renderer) como hijo.
        /// </summary>
        public static OttoPhone FindOrCreateOnOtto(OttoController otto)
        {
            if (otto == null) return Instance;

            var existing = otto.GetComponentInChildren<OttoPhone>();
            if (existing != null) return existing;

            // Crear uno vacío posicionado en la mano derecha de Otto.
            var go = new GameObject("OttoPhone");
            go.transform.SetParent(otto.transform, false);
            go.transform.localPosition = new Vector3(0.3f, -0.1f, 0f);
            return go.AddComponent<OttoPhone>();
        }

        // ── Coroutine ──────────────────────────────────────────────────────────
        private IEnumerator VibrateRoutine(Action onComplete)
        {
            _vibrating = true;
            TurnScreenOn();

            float pulseTime = vibrateDuration / Mathf.Max(vibrateCount, 1);
            for (int i = 0; i < vibrateCount; i++)
            {
                float elapsed = 0f;
                while (elapsed < pulseTime)
                {
                    float t     = elapsed / pulseTime;
                    float shake = Mathf.Sin(t * Mathf.PI * 10f) * vibrateIntensity * (1f - t * 0.4f);
                    transform.localPosition = _baseLPos + new Vector3(shake, shake * 0.5f, 0f);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            transform.localPosition = _baseLPos;
            _vibrating = false;
            onComplete?.Invoke();
        }
    }
}
