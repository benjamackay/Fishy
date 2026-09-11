using System;
using System.Linq;
using Fishy.Detective;
using Fishy.Mision;
using Fishy.Phone;
using Fishy.World;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Un objetivo concreto de una misión: juntar cierto objeto, hablar con cierto NPC
/// o atender cierto chat del celular.
///
/// Vive en el ensamblado por defecto (Assembly-CSharp) y NO dentro de la carpeta
/// Scripts/Mision, porque esa tiene el asmdef "Fishy.Mision" y desde ahí no se
/// pueden ver ni <see cref="ItemData"/> ni <see cref="NPC"/>. Las fichas de datos
/// de misión (DesafioData) siguen viviendo allá; esto es sólo el pegamento con
/// el mundo.
/// </summary>
/// <remarks>
/// <b>Un tipo nuevo va SIEMPRE al final del enum.</b> Unity los serializa por índice,
/// así que las escenas y prefabs guardan "tipo: 0/1/2". Meter uno en medio no da
/// ningún error: recablea en silencio todos los objetivos ya configurados, y un
/// "recoger objeto" pasa a ser un "hablar con NPC" sin que nadie se entere hasta
/// jugarlo.
/// </remarks>
public enum TipoObjetivo
{
    RecogerObjeto,
    HablarConNpc,
    ChatearPorTelefono,
    LlegarAZona,
    CompletarCasoDetective,
}

[Serializable]
public class ObjetivoMision
{
    public TipoObjetivo tipo = TipoObjetivo.RecogerObjeto;

    [Header("Si el tipo es Recoger Objeto")]
    [Tooltip("Qué hay que juntar. Es el mismo ItemData que lleva el WorldItem del mapa.")]
    public ItemData objeto;

    [Min(1)]
    [Tooltip("Cuántas unidades hacen falta.")]
    public int cantidad = 1;

    [Header("Si el tipo es Hablar Con Npc")]
    [Tooltip("Con quién hay que conversar para cumplirlo.")]
    public NPC npc;

    [Header("Si el tipo es Chatear Por Telefono")]
    [Tooltip("Qué conversación de celular hay que atender. Cuenta igual que hablar " +
             "con un NPC: se da por cumplido cuando el chat se cierra.")]
    public PhoneChatLauncher telefono;

    [Header("Si el tipo es Llegar A Zona")]
    [Tooltip("Id de la zona a la que hay que llegar: los mismos que usan ZonaMundo y " +
             "BlockedZone (zona_1, zona_2, zona_3). Se cumple en cuanto Otto entra, " +
             "y no se deshace si vuelve a salir.")]
    public string zonaDestino = "";

    [Header("Si el tipo es Completar Caso Detective")]
    [Tooltip("Qué caso hay que resolver. Cuenta con cualquier resultado, no hace " +
             "falta superar el umbral de aciertos: se da por cumplido cuando el " +
             "jugador cierra el caso.")]
    public DetectiveLauncher detective;

    [Header("Identificadores (los pone el catálogo; a mano se dejan vacíos)")]
    [Tooltip("itemId del objeto, para resolverlo por CatalogoItems cuando el objetivo " +
             "viene de la base o del archivo en vez de estar arrastrado aquí.")]
    public string itemId = "";

    [Tooltip("Id de diálogo del NPC (HDU1_SEC_COIPO_MASCOTA). Es lo que los NPCs del " +
             "mapa llevan en su campo 'dialogoId'.")]
    public string dialogoNpcId = "";

    [Tooltip("escenario_id del banco, separados por coma si son varias fases.")]
    public string escenarioIds = "";

    [Tooltip("caso_id del Modo Detective (DC_CASO_01), para resolver el objetivo por " +
             "dato cuando viene del catálogo en vez de estar arrastrado a mano.")]
    public string casoDetectiveId = "";


    /// <summary>
    /// Cumplido en esta sesión. No se serializa: el estado de la misión completa
    /// lo guarda MissionManager en PlayerPrefs, los objetivos sueltos no.
    /// </summary>
    [NonSerialized] public bool cumplido;

    /// <summary>
    /// Construye un objetivo a partir de lo que vino en los datos.
    ///
    /// Sólo traduce; no busca nada en la escena todavía. De eso se encarga
    /// <see cref="Resolver"/>, que se llama más tarde y tantas veces como haga falta:
    /// un NPC de otra zona puede no existir aún cuando el catálogo se carga.
    /// </summary>
    public static ObjetivoMision DesdeRegistro(ObjetivoRegistro registro)
    {
        if (registro == null) return null;

        var objetivo = new ObjetivoMision
        {
            tipo          = TipoDesdeTexto(registro.tipo),
            cantidad      = Mathf.Max(1, registro.cantidad),
            itemId        = registro.item_id ?? "",
            dialogoNpcId  = registro.dialogo_id ?? "",
            escenarioIds    = registro.escenario_ids ?? "",
            zonaDestino     = registro.zona_id ?? "",
            casoDetectiveId = registro.caso_id ?? "",
        };
        return objetivo;
    }

    /// <summary>
    /// Las cinco categorías en texto, tal como viajan en los datos. El texto no
    /// reconocido cae en <see cref="TipoObjetivo.RecogerObjeto"/> avisando: es mejor
    /// un objetivo que no se cumple y se ve raro en el panel que uno silenciosamente
    /// convertido en otra cosa.
    /// </summary>
    public static TipoObjetivo TipoDesdeTexto(string tipo)
    {
        switch ((tipo ?? "").Trim().ToLowerInvariant())
        {
            case "recoger_objeto":             return TipoObjetivo.RecogerObjeto;
            case "hablar_npc":                 return TipoObjetivo.HablarConNpc;
            case "chatear_telefono":           return TipoObjetivo.ChatearPorTelefono;
            case "llegar_zona":                return TipoObjetivo.LlegarAZona;
            case "completar_caso_detective":   return TipoObjetivo.CompletarCasoDetective;
            default:
                Debug.LogWarning($"[ObjetivoMision] Categoría de objetivo desconocida: " +
                                 $"'{tipo}'. Las válidas son recoger_objeto, hablar_npc, " +
                                 "chatear_telefono, llegar_zona y completar_caso_detective.");
                return TipoObjetivo.RecogerObjeto;
        }
    }

    /// <summary>
    /// Rellena las referencias del mundo a partir de los identificadores, si es que
    /// hacen falta. Devuelve true cuando el objetivo ya tiene con qué trabajar.
    ///
    /// <b>Lo que está puesto a mano manda.</b> Si alguien arrastró el NPC o el ItemData
    /// en el Inspector, no se toca: esto sólo rellena huecos.
    ///
    /// Se puede llamar muchas veces y es lo que se espera. Un NPC que todavía no se
    /// cargó hace que esto devuelva false, y el siguiente intento —cuando la zona ya
    /// esté abierta— lo encuentra. "Llegar a zona" no necesita resolver nada: su dato
    /// es el propio id.
    /// </summary>
    public bool Resolver()
    {
        switch (tipo)
        {
            case TipoObjetivo.RecogerObjeto:
                if (objeto == null && !string.IsNullOrWhiteSpace(itemId))
                    objeto = CatalogoItems.Buscar(itemId.Trim());
                return objeto != null;

            case TipoObjetivo.HablarConNpc:
                if (npc == null && !string.IsNullOrWhiteSpace(dialogoNpcId))
                    npc = BuscarNpcPorDialogo(dialogoNpcId.Trim());
                return npc != null;

            case TipoObjetivo.ChatearPorTelefono:
                if (telefono == null) telefono = BuscarLanzadorDeChat();
                return telefono != null;

            case TipoObjetivo.LlegarAZona:
                return !string.IsNullOrWhiteSpace(zonaDestino);

            case TipoObjetivo.CompletarCasoDetective:
                if (detective == null && !string.IsNullOrWhiteSpace(casoDetectiveId))
                    detective = BuscarDetectivePorCaso(casoDetectiveId.Trim());
                return detective != null;

            default:
                return false;
        }
    }

    /// <summary>
    /// El NPC del mapa cuyo <c>dialogoId</c> coincide. Se miran también los inactivos:
    /// los NPCs de zonas todavía cerradas suelen estar apagados, y un objetivo que
    /// apunta a uno de ellos tiene que poder resolverse antes de que la zona se abra.
    /// </summary>
    private static NPC BuscarNpcPorDialogo(string dialogoId)
    {
        foreach (NPC candidato in UnityEngine.Object.FindObjectsByType<NPC>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato == null) continue;
            if (string.Equals(candidato.dialogoId, dialogoId, StringComparison.Ordinal))
                return candidato;
        }
        return null;
    }

    /// <summary>
    /// El DetectiveLauncher cuyo caso_id coincide. Mismo criterio que
    /// BuscarNpcPorDialogo: se incluyen los inactivos, porque un caso de una zona
    /// todavía cerrada puede estar apagado cuando la misión se entrega.
    /// </summary>
    private static Fishy.Detective.DetectiveLauncher BuscarDetectivePorCaso(string casoId)
    {
        foreach (var candidato in UnityEngine.Object.FindObjectsByType<Fishy.Detective.DetectiveLauncher>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato == null) continue;
            if (string.Equals(candidato.CasoId, casoId, StringComparison.Ordinal))
                return candidato;
        }
        return null;
    }

    /// <summary>
    /// El lanzador de chat que corresponde a este objetivo: primero por escenario
    /// —que identifica la conversación— y si no se dio ninguno, por id de NPC de chat.
    ///
    /// El escenario va primero a propósito: el propio <c>PhoneChatLauncher</c> advierte
    /// que un mismo <c>npcId</c> se repite entre conversaciones distintas del banco, así
    /// que buscar por ahí puede enganchar la conversación equivocada.
    /// </summary>
    private PhoneChatLauncher BuscarLanzadorDeChat()
    {
        string[] escenarios = TrocearEscenarios(escenarioIds);

        foreach (PhoneChatLauncher candidato in UnityEngine.Object.FindObjectsByType<PhoneChatLauncher>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato == null) continue;

            if (escenarios.Length > 0)
            {
                string[] suyos = TrocearEscenarios(candidato.escenarioIds);
                foreach (string pedido in escenarios)
                    foreach (string suyo in suyos)
                        if (string.Equals(pedido, suyo, StringComparison.OrdinalIgnoreCase))
                            return candidato;
            }
        }

        // Antes había un respaldo que buscaba por npc_id cuando no cuadraba ningún
        // escenario. Se quitó con el enum 'source' de PhoneChatLauncher: el contenido
        // se elige siempre por escenario_id, que identifica la conversación en sí,
        // mientras que npc_id se repite entre conversaciones distintas y elegía mal.
        return null;
    }

    private static string[] TrocearEscenarios(string lista)
    {
        if (string.IsNullOrWhiteSpace(lista)) return Array.Empty<string>();
        return lista.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .ToArray();
    }

    /// <summary>Texto para el panel de misiones. Ej: "Juntar Concha (1/3)".</summary>
    public string Describir()
    {
        switch (tipo)
        {
            case TipoObjetivo.RecogerObjeto:
                // Sin objeto resuelto no se le puede preguntar al inventario, así que
                // se enseña el objetivo sin contador en vez de arriesgar un nulo: esto
                // pinta el panel, y el panel no puede tirar el juego abajo.
                if (objeto == null)
                    return $"Juntar {Respaldo(itemId, "objeto")} (0/{cantidad})";

                string nombreObjeto = !string.IsNullOrEmpty(objeto.itemName)
                    ? objeto.itemName
                    : Respaldo(itemId, "objeto");
                int tiene = InventoryManager.Instance != null
                    ? InventoryManager.Instance.GetQuantity(objeto)
                    : 0;
                return $"Juntar {nombreObjeto} ({Mathf.Min(tiene, cantidad)}/{cantidad})";

            case TipoObjetivo.HablarConNpc:
                return $"Hablar con {NombreVisibleDe(npc) ?? Respaldo(dialogoNpcId, "NPC")}";

            case TipoObjetivo.ChatearPorTelefono:
                string nombreChat = telefono != null
                    ? telefono.name
                    : Respaldo(escenarioIds, "chat");
                return $"Atender el chat de {nombreChat}";

            case TipoObjetivo.LlegarAZona:
                if (string.IsNullOrWhiteSpace(zonaDestino)) return "Ir a (zona sin asignar)";
                return $"Ir a {ZonaMundo.NombreDe(zonaDestino)}";

            case TipoObjetivo.CompletarCasoDetective:
                return "Resolver un caso del Modo Detective";

            default:
                return "(objetivo desconocido)";
        }
    }

    /// <summary>Lo que se muestra cuando la referencia no está resuelta: el propio id,
    /// que al menos dice de qué se trata, o una etiqueta genérica si tampoco hay id.</summary>
    private static string Respaldo(string id, string queEs) =>
        string.IsNullOrWhiteSpace(id) ? $"({queEs} sin asignar)" : id.Trim();

    /// <summary>
    /// Cómo se llama este NPC para el niño/a, o null si no hay NPC.
    ///
    /// Se prefiere el nombre del diálogo —que <c>NPC.Awake</c> rellena desde el banco,
    /// así que dice "Huemul"— antes que el nombre del GameObject, que dice cosas como
    /// "Neutral_NPC (1)". En el cartel de misión lo lee un niño/a, no quien montó la
    /// escena.
    /// </summary>
    private static string NombreVisibleDe(NPC npc)
    {
        if (npc == null) return null;

        if (npc.dialogueData != null && !string.IsNullOrWhiteSpace(npc.dialogueData.npcName))
            return npc.dialogueData.npcName.Trim();

        return npc.name;
    }

    /// <summary>Comprueba contra el mundo si este objetivo ya está cumplido.</summary>
    public bool Evaluar()
    {
        if (cumplido) return true;

        // "Hablar con" y "chatear por teléfono" no se pueden consultar: son hechos
        // puntuales, los marca MissionTracker cuando se cierra el diálogo o el chat.
        // Tampoco se intenta resolverlos aquí: buscarlos es recorrer la escena, y esto
        // se llama en cada cambio de inventario. De reintentar ESOS se encarga
        // MissionTracker, que es quien necesita el resultado para suscribirse.
        if (tipo == TipoObjetivo.RecogerObjeto)
        {
            // Resolver el objeto sí es barato —es una consulta al catálogo— y hace
            // falta en cada intento: puede que el catálogo no estuviera cargado cuando
            // se entregó la misión.
            if (objeto == null) Resolver();

            if (objeto != null && InventoryManager.Instance != null)
                cumplido = InventoryManager.Instance.GetQuantity(objeto) >= cantidad;
        }

        // Llegar a una zona sí se puede consultar, y por eso se consulta: preguntarle
        // a ZonaActual da la respuesta correcta también cuando la misión se entrega
        // estando Otto YA dentro de la zona, que con un evento de entrada se quedaría
        // esperando para siempre a que saliera y volviera a entrar.
        else if (tipo == TipoObjetivo.LlegarAZona && !string.IsNullOrWhiteSpace(zonaDestino))
        {
            ZonaActual zonas = ZonaActual.Instance;
            if (zonas != null) cumplido = zonas.Actual == zonaDestino.Trim();
        }

        return cumplido;
    }

    /// <summary>
    /// Evento cuya invocación da por cumplido este objetivo, o null si no se cumple
    /// por evento sino consultando el mundo (RecogerObjeto, LlegarAZona).
    ///
    /// A los que devuelven null hay que darles un motivo para que alguien los vuelva
    /// a consultar: el inventario y el cambio de zona, a los que MissionTracker se
    /// suscribe una sola vez para todos.
    ///
    /// Vive aquí y no en MissionTracker para que sumar un tipo de objetivo nuevo sea
    /// tocar un solo archivo.
    /// </summary>
    public UnityEvent EventoQueLoCumple()
    {
        switch (tipo)
        {
            case TipoObjetivo.HablarConNpc:
                return npc != null ? npc.onDialogueEnded : null;

            case TipoObjetivo.ChatearPorTelefono:
                return telefono != null ? telefono.onChatClosed : null;

            case TipoObjetivo.CompletarCasoDetective:
                return detective != null ? detective.onCasoResuelto : null;

            default:
                return null;
        }
    }
}
