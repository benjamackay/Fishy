using System;
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

    /// <summary>
    /// Cumplido en esta sesión. No se serializa: el estado de la misión completa
    /// lo guarda MissionManager en PlayerPrefs, los objetivos sueltos no.
    /// </summary>
    [NonSerialized] public bool cumplido;

    /// <summary>Texto para el panel de misiones. Ej: "Juntar Concha (1/3)".</summary>
    public string Describir()
    {
        switch (tipo)
        {
            case TipoObjetivo.RecogerObjeto:
                string nombreObjeto = objeto != null && !string.IsNullOrEmpty(objeto.itemName)
                    ? objeto.itemName
                    : "(objeto sin asignar)";
                int tiene = InventoryManager.Instance.GetQuantity(objeto);
                return $"Juntar {nombreObjeto} ({Mathf.Min(tiene, cantidad)}/{cantidad})";

            case TipoObjetivo.HablarConNpc:
                string nombreNpc = npc != null ? npc.name : "(NPC sin asignar)";
                return $"Hablar con {nombreNpc}";

            case TipoObjetivo.ChatearPorTelefono:
                string nombreChat = telefono != null ? telefono.name : "(chat sin asignar)";
                return $"Atender el chat de {nombreChat}";

            case TipoObjetivo.LlegarAZona:
                if (string.IsNullOrWhiteSpace(zonaDestino)) return "Ir a (zona sin asignar)";
                return $"Ir a {ZonaMundo.NombreDe(zonaDestino)}";

            default:
                return "(objetivo desconocido)";
        }
    }

    /// <summary>Comprueba contra el mundo si este objetivo ya está cumplido.</summary>
    public bool Evaluar()
    {
        if (cumplido) return true;

        // "Hablar con" y "chatear por teléfono" no se pueden consultar: son hechos
        // puntuales, los marca MissionTracker cuando se cierra el diálogo o el chat.
        if (tipo == TipoObjetivo.RecogerObjeto && objeto != null)
            cumplido = InventoryManager.Instance.GetQuantity(objeto) >= cantidad;

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

            default:
                return null;
        }
    }
}
