using Fishy.Mision;
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

        // Por si el panel ya se actualizó (partida restaurada) antes de que este
        // objeto llegara a suscribirse.
        RevisarCatalogo();
    }

    private void OnDisable()
    {
        if (manager != null) manager.onPanelActualizado.RemoveListener(RevisarCatalogo);
        CatalogoMisiones.OnCatalogoCambiado -= RevisarCatalogo;
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

            if (!string.IsNullOrWhiteSpace(registro.desbloquea_mision))
                EntregarMisionDelCatalogo.EntregarPorId(registro.desbloquea_mision);

            if (!string.IsNullOrWhiteSpace(registro.recompensa_item_id))
                EntregarRecompensa(registro);
        }
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
