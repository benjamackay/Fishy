using Fishy.World;
using UnityEngine;

/// <summary>
/// Entrega una misión del catálogo en cuanto Otto entra a una zona, sin que haga
/// falta hablar con nadie.
///
/// No hace la entrega él mismo: se apoya en <see cref="EntregarMisionDelCatalogo"/>,
/// que ya sabe registrar la misión y engancharle los objetivos. Este componente sólo
/// decide CUÁNDO — así "entregar por id" sigue viviendo en un solo sitio, igual que
/// lo reutiliza <see cref="AlCompletarMision"/> para "cuando termine otra misión".
///
/// No necesita collider ni estar cerca de nada: <see cref="ZonaActual.OnZonaCambiada"/>
/// es un evento global, así que este GameObject puede vivir en cualquier parte de la
/// escena, incluso junto a otros disparadores de misión.
/// </summary>
[RequireComponent(typeof(EntregarMisionDelCatalogo))]
public class EntregarMisionAlEntrarZona : MonoBehaviour
{
    [Tooltip("Zona espacial que dispara la entrega: los mismos ids que usan ZonaMundo " +
             "y BlockedZone (zona_1, zona_2, zona_3).")]
    public string zonaId = "";

    [Tooltip("Escribir en consola cuándo se disparó.")]
    public bool verboseLogs = true;

    /// <summary>Ya se entregó en esta sesión. No vuelve a intentarlo si Otto sale y
    /// vuelve a entrar a la misma zona.</summary>
    public bool YaDisparado { get; private set; }

    private EntregarMisionDelCatalogo entregador;

    private void Awake() => entregador = GetComponent<EntregarMisionDelCatalogo>();

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(zonaId))
        {
            Debug.LogWarning($"[{name}] EntregarMisionAlEntrarZona sin 'Zona Id': " +
                             "no va a disparar nunca.", this);
            return;
        }

        ZonaActual.OnZonaCambiada += AlCambiarDeZona;

        // Por si Otto YA está en la zona cuando este objeto se habilita: una partida
        // que se carga dentro de la zona dispara OnZonaCambiada antes de que este
        // componente llegue a suscribirse, y sin esto se quedaría esperando para
        // siempre a una entrada que ya pasó.
        ZonaActual zonas = ZonaActual.Instance;
        if (zonas != null) AlCambiarDeZona("", zonas.Actual);
    }

    private void OnDisable() => ZonaActual.OnZonaCambiada -= AlCambiarDeZona;

    private void AlCambiarDeZona(string anterior, string nueva)
    {
        if (YaDisparado) return;
        if (nueva != zonaId.Trim()) return;

        YaDisparado = true;

        if (verboseLogs)
            Debug.Log($"[EntregarMisionAlEntrarZona] Otto entró a '{zonaId}', " +
                      $"disparando '{name}'.", this);

        entregador.Entregar();
    }

    /// <summary>Rearma el disparador. Solo para pruebas y para el editor: en una
    /// partida normal la entrega pasa una vez.</summary>
    public void Rearmar() => YaDisparado = false;
}
