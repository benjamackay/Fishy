using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

/// <summary>
/// HDU-1 CA2 — recuerda qué objetos del mapa ya recogió el niño/a, para que no
/// vuelvan a aparecer en el suelo al retomar la partida.
///
/// <b>Por qué no basta con el inventario.</b> Lo primero que uno piensa es "si lo
/// tiene, lo recogió". No sirve: <c>ItemData.ItemType.Consumable</c> significa que
/// un objeto usado sale de la mochila, y entonces el del suelo reaparecería. Y dos
/// <see cref="WorldItem"/> distintos pueden entregar el mismo <c>ItemData</c>, así
/// que hay que identificar el objeto de la escena, no el ítem.
///
/// <b>Por qué la API es estática.</b> Los <see cref="WorldItem"/> preguntan en su
/// propio <c>Start()</c>, uno por uno y antes del primer frame. Un singleton que
/// haya que buscar con <c>FindAnyObjectByType</c> en cada Start sería una búsqueda
/// por objeto del mapa; un diccionario estático se consulta en tiempo constante.
///
/// A diferencia del inventario, esto <b>solo crece</b>: recoger es un camino de ida.
/// Por eso se manda un POST por objeto en vez de la lista entera.
/// </summary>
public class ObjetosRecogidosSync : MonoBehaviour
{
    public static ObjetosRecogidosSync Instance { get; private set; }

    private const float EsperaEntreIntentos = 0.5f;
    private const float SegundosAntesDeAvisarQueNoHayPartida = 8f;

    /// <summary>
    /// Lo que esta partida ya recogió. Estático para que un <see cref="WorldItem"/>
    /// pueda preguntar desde su Start sin buscar nada en la escena.
    /// </summary>
    private static readonly HashSet<string> yaRecogidos = new HashSet<string>();

    /// <summary>
    /// Hasta que la respuesta no llega, no se sabe nada: preguntar devuelve false y
    /// los objetos se quedan en el mapa. Es lo correcto — el error caro sería el
    /// contrario, esconder objetos que el niño/a nunca recogió.
    /// </summary>
    private static bool registroCargado;

    private static int? partidaCargada;
    private bool avisoDeSinPartidaDado;

    /// <summary>
    /// Objetos recogidos antes de que hubiera partida o conexión. Se mandan cuando
    /// se pueda: perder el registro de algo que el niño/a ya tiene en la mochila
    /// dejaría el objeto de vuelta en el suelo la próxima vez.
    /// </summary>
    private static readonly List<string> pendientesDeSubir = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarEstadoEstatico()
    {
        // Los estáticos sobreviven al Stop del editor con "Enter Play Mode Options"
        // activado. Sin esto, la segunda corrida arrancaría creyendo que ya sabe qué
        // se recogió, y escondería objetos de la partida anterior.
        yaRecogidos.Clear();
        pendientesDeSubir.Clear();
        registroCargado = false;
        partidaCargada = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        if (Instance != null) return;
        var go = new GameObject("ObjetosRecogidosSync");
        go.AddComponent<ObjetosRecogidosSync>();
    }

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

    private void OnEnable() { StartCoroutine(EsperarPartida()); }

    // ── Lo que consultan los WorldItem ───────────────────────────────────────

    /// <summary>
    /// True si ese objeto ya se recogió en esta partida. Con el registro sin cargar
    /// devuelve false: mejor un objeto de más en el mapa que uno escondido de menos.
    /// </summary>
    public static bool YaFueRecogido(string objetoId)
    {
        if (!registroCargado || string.IsNullOrWhiteSpace(objetoId)) return false;
        return yaRecogidos.Contains(objetoId.Trim());
    }

    /// <summary>Registra que se recogió, y lo sube. Idempotente.</summary>
    public static void Marcar(string objetoId, string nombreParaElAviso = null)
    {
        if (string.IsNullOrWhiteSpace(objetoId))
        {
            // Sin id no se puede recordar, y el objeto va a reaparecer la próxima vez.
            // Se avisa en vez de dejarlo pasar callado.
            Debug.LogWarning(
                $"[ObjetosRecogidos] '{nombreParaElAviso ?? "(sin nombre)"}' no tiene objetoId, " +
                "así que va a volver a aparecer en el mapa. Corre Fishy → Asignar ids a los " +
                "objetos del mapa.");
            return;
        }

        string id = objetoId.Trim();
        if (!yaRecogidos.Add(id)) return;   // ya estaba

        Subir(id);
    }

    private static void Subir(string id)
    {
        var api = ApiManager.Instance;
        if (api == null || api.PartidaId == null)
        {
            if (!pendientesDeSubir.Contains(id)) pendientesDeSubir.Add(id);
            return;
        }

        api.MarcarObjetoRecogido(id,
            onSuccess: _ => pendientesDeSubir.Remove(id),
            onError: e =>
            {
                if (!pendientesDeSubir.Contains(id)) pendientesDeSubir.Add(id);
                Debug.LogWarning($"[ObjetosRecogidos] No se pudo guardar '{id}': {e}. Se reintentará.");
            });
    }

    // ── Bajar el registro ────────────────────────────────────────────────────

    private IEnumerator EsperarPartida()
    {
        var espera = new WaitForSeconds(EsperaEntreIntentos);
        float sinPartidaDesde = Time.realtimeSinceStartup;

        while (true)
        {
            var api = ApiManager.Instance;

            if (api == null || api.PartidaId == null)
            {
                if (!avisoDeSinPartidaDado &&
                    Time.realtimeSinceStartup - sinPartidaDesde > SegundosAntesDeAvisarQueNoHayPartida)
                {
                    avisoDeSinPartidaDado = true;
                    Debug.LogWarning(
                        "[ObjetosRecogidos] Llevo varios segundos sin PartidaId: los objetos que " +
                        "recoja el niño/a van a reaparecer la próxima vez. Si estás probando, entra " +
                        "por MenuUno para pasar por el login.");
                }
            }
            else
            {
                sinPartidaDesde = Time.realtimeSinceStartup;
                avisoDeSinPartidaDado = false;

                if (partidaCargada != api.PartidaId)
                {
                    partidaCargada = api.PartidaId;
                    registroCargado = false;
                    yaRecogidos.Clear();
                    Bajar();
                }

                // Lo que quedó pendiente por falta de partida o de red, se reintenta.
                if (pendientesDeSubir.Count > 0)
                {
                    foreach (var id in new List<string>(pendientesDeSubir)) Subir(id);
                }
            }

            yield return espera;
        }
    }

    private void Bajar()
    {
        var api = ApiManager.Instance;
        if (api == null) return;

        api.ObtenerObjetosRecogidos(
            onSuccess: lista =>
            {
                if (lista != null)
                {
                    foreach (var o in lista)
                        if (o != null && !string.IsNullOrEmpty(o.objeto_id))
                            yaRecogidos.Add(o.objeto_id);
                }
                registroCargado = true;

                // Los WorldItem que ya corrieron su Start (porque la escena cargó antes
                // de que llegara la respuesta) hay que apagarlos ahora. Aquí sí puede
                // verse el parpadeo; el caso normal es que la respuesta llegue mientras
                // el niño/a todavía está en el menú.
                int apagados = ApagarLosQueYaEstan();

                Debug.Log($"[ObjetosRecogidos] {yaRecogidos.Count} objeto(s) ya recogidos" +
                          (apagados > 0 ? $"; {apagados} quitados del mapa ahora." : "."));
            },
            onError: e =>
            {
                // Sin registro no se esconde nada: los objetos quedan en el mapa y el
                // niño/a puede recogerlos de nuevo. Es molesto, no destructivo.
                Debug.LogWarning($"[ObjetosRecogidos] No se pudo saber qué estaba recogido: {e}");
            });
    }

    /// <summary>Quita del mapa los WorldItem que ya estaban puestos cuando llegó el registro.</summary>
    private static int ApagarLosQueYaEstan()
    {
        int apagados = 0;
        foreach (var item in FindObjectsByType<WorldItem>())
        {
            if (item == null || !item.CanInteract()) continue;
            if (!YaFueRecogido(item.objetoId)) continue;

            item.QuitarDelMapa();
            apagados++;
        }
        return apagados;
    }
}
