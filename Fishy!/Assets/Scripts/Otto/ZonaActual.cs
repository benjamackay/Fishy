using System;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// Sabe en qué zona está Otto ahora mismo, y avisa cuando cambia.
    ///
    /// Es la pieza que faltaba para HDU-15: el backend no guarda `zona_actual`
    /// porque, en sus palabras, "hoy Unity no tiene el concepto de zona en la que
    /// está Otto — las BlockedZone saben abrirse, no saben contener". Esto es ese
    /// concepto. Con él se puede guardar la zona al salir, restaurarla al volver y
    /// disparar el guardado automático al cambiar de zona.
    ///
    /// <b>Por qué consulta en vez de escuchar triggers.</b> Las zonas se dibujan con
    /// colliders SÓLIDOS que hacen de barrera, y BlockedZone los desactiva al
    /// desbloquear. Convertirlos en triggers rompería el bloqueo, y duplicarlos
    /// obligaría a redibujar cada polígono a mano. Preguntar por la posición cada
    /// pocas décimas cuesta una prueba punto-en-polígono por zona —hoy dos— y no
    /// toca nada de lo que ya funciona.
    ///
    /// Se crea solo: no hace falta ponerlo en la escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZonaActual : MonoBehaviour
    {
        public static ZonaActual Instance { get; private set; }

        [Header("Configuración")]
        [Tooltip("Zona en la que está Otto cuando no cae dentro de ninguna otra. Es el " +
                 "mapa de partida, que no tiene polígono porque no se bloquea nunca.")]
        public string zonaPorDefecto = "zona_1";

        [Tooltip("Cada cuántos segundos se comprueba. Subirlo ahorra trabajo; bajarlo " +
                 "hace que el cambio de zona se note antes.")]
        [Min(0.05f)]
        public float intervalo = 0.25f;

        [Tooltip("Escribir en consola cada cambio de zona.")]
        public bool verboseLogs = true;

        /// <summary>Zona donde está Otto. Vacío hasta la primera comprobación.</summary>
        public string Actual { get; private set; } = "";

        /// <summary>
        /// (anterior, nueva). Estático a propósito: quien quiera guardar al cambiar de
        /// zona se suscribe una vez y no depende de que este objeto ya exista.
        /// </summary>
        public static event Action<string, string> OnZonaCambiada;

        private Transform _otto;
        private float _proximaRevision;

        public static ZonaActual GetOrCreate()
        {
            if (Instance != null) return Instance;

            var encontrado = FindAnyObjectByType<ZonaActual>();
            if (encontrado != null) return encontrado;

            var go = new GameObject("ZonaActual");
            return go.AddComponent<ZonaActual>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _proximaRevision) return;
            _proximaRevision = Time.unscaledTime + intervalo;

            Revisar();
        }

        /// <summary>Comprueba ya, sin esperar al siguiente tic. Útil justo antes de
        /// guardar, para no escribir una zona con un cuarto de segundo de retraso.</summary>
        public string Revisar()
        {
            if (_otto == null)
            {
                var otto = FindAnyObjectByType<OttoController>();
                if (otto == null) return Actual;
                _otto = otto.transform;
            }

            string zona = ZonaEn(_otto.position);
            if (zona == Actual) return Actual;

            string anterior = Actual;
            Actual = zona;

            if (verboseLogs)
                Debug.Log($"[ZonaActual] {(string.IsNullOrEmpty(anterior) ? "(inicio)" : anterior)} → {zona}", this);

            OnZonaCambiada?.Invoke(anterior, zona);
            return Actual;
        }

        /// <summary>
        /// Zona que contiene ese punto, o <see cref="zonaPorDefecto"/> si ninguna.
        ///
        /// Si dos zonas se solapan gana la última registrada, que es arbitrario a
        /// propósito: las zonas del mapa no deberían solaparse, y si lo hacen es un
        /// error de montaje que conviene ver en vez de resolver en silencio.
        /// </summary>
        public string ZonaEn(Vector2 punto)
        {
            string encontrada = null;
            int cuantas = 0;

            foreach (ZonaMundo zona in ZonaMundo.Vivas)
            {
                if (zona == null || !zona.Contiene(punto)) continue;
                encontrada = zona.Id;
                cuantas++;
            }

            if (cuantas > 1 && verboseLogs)
            {
                Debug.LogWarning($"[ZonaActual] El punto {punto} cae dentro de {cuantas} zonas " +
                                 $"a la vez. Se queda con '{encontrada}', pero los polígonos " +
                                 "se están pisando.", this);
            }

            return encontrada ?? zonaPorDefecto;
        }
    }
}
