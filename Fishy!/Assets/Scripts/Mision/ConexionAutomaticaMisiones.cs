using System;
using Fishy.Mision;
using Fishy.Net;
using UnityEngine;

/// <summary>
/// Conecta solo el catálogo de misiones: si una misión completada trae
/// <c>desbloquea_mision</c> o <c>recompensa_item_id</c> (en
/// <c>Resources/misiones.json</c> o, el día que exista, en la base), esto entrega lo
/// que corresponda sin que haga falta poner nada a mano en la escena.
///
/// <b>No reemplaza a los disparadores de escena.</b> <see cref="AlCompletarMision"/>
/// + <see cref="EntregarMisionDelCatalogo"/> y <see cref="EntregarMisionAlEntrarZona"/>
/// siguen siendo el camino para lo que necesita algo más que "dar esta misión" o
/// "dar este ítem" —cinemática, mensaje propio, desbloqueo de zona—. Esto cubre el
/// caso simple, así que encadenar una misión nueva o darle una recompensa no obliga
/// a tocar ninguna escena: basta con llenar esos dos campos en el catálogo. Una
/// misión que necesite el camino manual simplemente los deja vacíos.
///
/// <b>Por qué barre TODO el catálogo en cada refresco, en vez de suscribirse a una
/// misión a la vez.</b> Es el mismo problema de fondo que resuelve
/// <see cref="DisparadorDeMision"/> —hay que enterarse tanto de lo que se completa en
/// vivo como de lo que ya venía completo al restaurar la partida—, resuelto distinto
/// porque aquí no hay nada que celebrar con cinemática, solo entregar: no hace falta
/// el camino doble "en vivo" / "al restaurar", basta con revisar el catálogo entero
/// cada vez que el panel se actualiza. Tanto <c>EntregarMisionDelCatalogo.EntregarPorId</c>
/// como el otorgamiento de ítems son idempotentes (comprueban el estado antes de
/// actuar), así que repetir la revisión en cada refresco es seguro, igual que ya lo
/// es <c>MisionInicial.Entregar</c>.
///
/// Se crea sola al arrancar, como <c>MisionCatalogoSync</c> y
/// <c>MissionUIController</c>: no hay que montar nada en la escena.
/// </summary>
public class ConexionAutomaticaMisiones : MonoBehaviour
{
    public static ConexionAutomaticaMisiones Instance { get; private set; }

    [Tooltip("Escribir en consola cada recompensa automática entregada.")]
    public bool verboseLogs = true;

    private MissionManager manager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<ConexionAutomaticaMisiones>() != null) return;

        new GameObject(nameof(ConexionAutomaticaMisiones)).AddComponent<ConexionAutomaticaMisiones>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        manager = MissionManager.GetOrCreate();
        manager.onPanelActualizado.AddListener(RevisarCatalogo);
        CatalogoMisiones.OnCatalogoCambiado += RevisarCatalogo;

        // Una bifurcación que se quedó esperando a la restauración (ver
        // ElegirSiguiente) se resuelve aquí, cuando ya no queda nada por llegar.
        MisionBackendSync.OnProgresoDeMisionesAplicado += RevisarCatalogo;

        // Por si el panel ya se actualizó (partida restaurada) antes de que este
        // objeto llegara a suscribirse.
        RevisarCatalogo();
    }

    private void OnDisable()
    {
        if (manager != null) manager.onPanelActualizado.RemoveListener(RevisarCatalogo);
        CatalogoMisiones.OnCatalogoCambiado -= RevisarCatalogo;
        MisionBackendSync.OnProgresoDeMisionesAplicado -= RevisarCatalogo;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void RevisarCatalogo()
    {
        foreach (MisionRegistro registro in CatalogoMisiones.Todas.Values)
        {
            if (!manager.EstaCompletado(registro.mision_id)) continue;

            string siguiente = ElegirSiguiente(
                registro,
                yaEntregada: id => manager.GetEstado(id) != null,
                rechazo: RechazoEnSuChat(registro),
                puedeEsperar: QuedaProgresoPorRestaurar());

            if (!string.IsNullOrWhiteSpace(siguiente))
                EntregarMisionDelCatalogo.EntregarPorId(siguiente);

            if (!string.IsNullOrWhiteSpace(registro.recompensa_item_id))
                EntregarRecompensa(registro);
        }
    }

    /// <summary>
    /// Qué misión sigue a <paramref name="registro"/>, ya completada. Null = nada, o
    /// todavía no se puede saber.
    ///
    /// Sin bifurcación (<c>desbloquea_si_acepta</c> vacío) es siempre
    /// <c>desbloquea_mision</c>. Con bifurcación, en este orden:
    ///
    ///  1. <b>Si una de las dos ramas ya se entregó, esa.</b> La decisión se toma una
    ///     sola vez, en vivo, y queda guardada en el propio progreso de misiones. Este
    ///     método se llama en cada refresco del panel, y sin esto podría volver a
    ///     decidir —con otra información— y entregar también la otra rama.
    ///  2. <b>Si se sabe cómo terminó el reto, la rama que corresponda.</b>
    ///  3. <b>Si no se sabe y la partida se está restaurando, esperar.</b> Al cargar,
    ///     las misiones se registran de a una: la completada puede llegar antes que la
    ///     rama que ya se había elegido. Decidir ahí sería adivinar con la respuesta a
    ///     un paso de llegar.
    ///  4. <b>Si no se sabe y no queda nada por llegar, la rama de "no rechazó".</b>
    ///     Pasa si el chat se cerró sin elegir nada, o si la partida sigue en otro
    ///     equipo justo después del reto. Va por ese lado porque las escenas de la otra
    ///     rama dan por hecho el rechazo ("ya van dos veces que dices que no"), y es la
    ///     misma lectura conservadora de <c>VariablesJugador</c>: sin datos, no hay racha.
    ///
    /// Estática y con todo por parámetro para poder probarla sin escena.
    /// </summary>
    public static string ElegirSiguiente(MisionRegistro registro, Func<string, bool> yaEntregada,
        bool? rechazo, bool puedeEsperar)
    {
        if (registro == null) return null;

        string siRechaza = Limpio(registro.desbloquea_mision);
        string siAcepta  = Limpio(registro.desbloquea_si_acepta);
        if (siAcepta == null) return siRechaza;

        if (siRechaza != null && yaEntregada(siRechaza)) return siRechaza;
        if (yaEntregada(siAcepta)) return siAcepta;

        if (rechazo.HasValue) return rechazo.Value ? siRechaza : siAcepta;
        if (puedeEsperar) return null;
        return siAcepta;
    }

    private static string Limpio(string id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

    /// <summary>Cómo terminó el chat de esta misión: el de su último objetivo de chat,
    /// que en un reto es la conversación que lo decide.</summary>
    private static bool? RechazoEnSuChat(MisionRegistro registro)
    {
        if (string.IsNullOrWhiteSpace(registro.desbloquea_si_acepta)) return null;

        bool? rechazo = null;
        foreach (ObjetivoRegistro objetivo in registro.ObjetivosEnOrden())
        {
            if (!string.Equals(objetivo.tipo?.Trim(), "chatear_telefono", StringComparison.OrdinalIgnoreCase))
                continue;
            bool? este = RechazosEnChats.Rechazo(objetivo.escenario_ids);
            if (este.HasValue) rechazo = este;
        }
        return rechazo;
    }

    /// <summary>Hay servidor y su progreso todavía no se aplicó: puede faltar la rama
    /// que ya se había elegido en otra sesión.</summary>
    private static bool QuedaProgresoPorRestaurar()
    {
        ApiManager api = ApiManager.Instance;
        bool hayServidor = api != null && !api.IsLocalMode && api.IsLoggedIn && api.PartidaId != null;
        return hayServidor && !MisionBackendSync.ProgresoDeMisionesAplicado;
    }

    private void EntregarRecompensa(MisionRegistro registro)
    {
        ItemData item = CatalogoItems.Buscar(registro.recompensa_item_id.Trim());
        if (item == null)
        {
            Debug.LogWarning($"[ConexionAutomaticaMisiones] '{registro.mision_id}' apunta a " +
                             $"'{registro.recompensa_item_id}' como recompensa, pero no hay " +
                             "ningún ItemData con ese id en Resources/Items.");
            return;
        }

        InventoryManager inventario = InventoryManager.Instance;
        if (inventario == null) return;

        // Guard necesario: InventoryManager.AddItem suma cantidad, no la fija. Las
        // misiones no se repiten, así que basta con "ya la tiene" para no volver a
        // sumarla — no hace falta el "no duplica al repetir" del Modo Detective,
        // porque aquí no hay caso que se pueda rejugar.
        if (inventario.GetQuantity(item) > 0) return;

        int cantidad = registro.recompensa_cantidad > 0 ? registro.recompensa_cantidad : 1;
        inventario.AddItem(item, cantidad);

        if (verboseLogs)
            Debug.Log($"[ConexionAutomaticaMisiones] Recompensa '{registro.recompensa_item_id}' " +
                      $"entregada por completar '{registro.mision_id}'.");
    }
}
