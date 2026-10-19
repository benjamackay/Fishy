using System.Collections;
using System.Collections.Generic;
using Fishy.Mision;
using Fishy.Net;
using UnityEngine;

/// <summary>
/// Baja el catálogo de misiones desde la base al arrancar, para que las misiones y
/// sus objetivos salgan de la BD y no de la copia empaquetada.
///
/// <b>El juego nunca espera a esto para funcionar.</b> <see cref="CatalogoMisiones"/>
/// ya tiene cargado <c>Resources/misiones.json</c> desde el primer frame, así que un
/// NPC al que se le hable antes de que llegue la respuesta entrega su misión igual.
/// Cuando la descarga llega, reemplaza el catálogo y avisa; los MissionGiver que
/// todavía no entregaron nada se vuelven a resolver solos.
///
/// Si no hay red, no hay sesión, se juega en modo local o el endpoint todavía no
/// existe, no pasa nada: se sigue con el archivo. Ése es el respaldo.
///
/// Es contenido global, no progreso: no depende de la partida y se baja una sola vez
/// por ejecución. El progreso de misiones sigue siendo cosa de
/// <c>MisionBackendSync</c>, que es otra cosa y va por su lado.
///
/// Está calcado de <c>BancoBackendSync</c> a propósito: mismo problema, misma forma.
/// </summary>
[DisallowMultipleComponent]
public class MisionCatalogoSync : MonoBehaviour
{
    public static MisionCatalogoSync Instance { get; private set; }

    [Tooltip("Segundos entre intentos mientras se espera a que haya sesión iniciada.")]
    [Min(0.5f)]
    public float esperaEntreIntentos = 1f;

    [Tooltip("Cuántos segundos esperar a que aparezca la sesión antes de rendirse y " +
             "quedarse con el archivo de respaldo. 0 = esperar siempre.")]
    [Min(0f)]
    public float tiempoMaximoDeEspera = 30f;

    [Tooltip("Escribir en consola el resultado de la descarga.")]
    public bool verboseLogs = true;

    /// <summary>True cuando el catálogo en memoria ya viene de la base.</summary>
    public bool DescargadoDeLaBase => CatalogoMisiones.DeDonde == CatalogoMisiones.Origen.Base;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<MisionCatalogoSync>() != null) return;

        new GameObject("MisionCatalogoSync").AddComponent<MisionCatalogoSync>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Que el archivo se lea YA, sin esperar a que alguien pregunte. Así, si la
        // descarga falla, el hueco ya está tapado antes de que haga falta.
        _ = CatalogoMisiones.Todas;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start() => StartCoroutine(Bajar());

    private IEnumerator Bajar()
    {
        float esperado = 0f;

        // La sesión se inicia en la pantalla de acceso, que puede ir por delante o por
        // detrás de esta escena según cómo se arranque el juego. Se espera en vez de
        // darlo por perdido en el primer frame.
        while (true)
        {
            var api = ApiManager.Instance;
            if (api != null && !api.IsLocalMode && api.IsLoggedIn) break;

            if (tiempoMaximoDeEspera > 0f && esperado >= tiempoMaximoDeEspera)
            {
                if (verboseLogs)
                    Debug.Log($"[MisionCatalogoSync] Sin sesión tras {tiempoMaximoDeEspera:F0}s: " +
                              "las misiones salen del archivo de respaldo.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(esperaEntreIntentos);
            esperado += esperaEntreIntentos;
        }

        ApiManager.Instance.ObtenerCatalogoMisiones(
            onSuccess: misiones =>
            {
                CatalogoMisiones.AplicarDesdeBase(misiones);
                if (verboseLogs)
                    Debug.Log($"[MisionCatalogoSync] Catálogo bajado de la base " +
                              $"({(misiones != null ? misiones.Count : 0)} misión(es)).", this);
            },
            onError: e => Debug.LogWarning(
                $"[MisionCatalogoSync] No se pudo bajar el catálogo de misiones ({e}); " +
                "se sigue con Resources/misiones.json."));
    }

    /// <summary>Vuelve a pedir el catálogo. Para el editor y las pruebas: en una
    /// partida normal se baja una vez al arrancar.</summary>
    public void Rebajar()
    {
        StopAllCoroutines();
        StartCoroutine(Bajar());
    }

    /// <summary>
    /// Fuerza el respaldo: descarta lo que haya y relee el archivo. Sirve para probar
    /// en el editor cómo se comporta el juego sin base de datos, sin tener que apagar
    /// el servidor.
    /// </summary>
    [ContextMenu("Usar solo el archivo de respaldo")]
    public void ForzarRespaldo()
    {
        StopAllCoroutines();
        CatalogoMisiones.Recargar();
        Debug.Log($"[MisionCatalogoSync] Forzado el archivo de respaldo: " +
                  $"{CatalogoMisiones.Todas.Count} misión(es).", this);
    }
}
