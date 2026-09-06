using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;
using Fishy.Zonas.BosqueDesconocidos;

/// <summary>
/// HDU-3 CA5 / HDU-4 CA5 — recuerda con qué NPCs de una temática ya terminó el
/// niño/a, para que la temática se pueda completar en varias sesiones.
///
/// <b>El problema que resuelve.</b> `BosqueDesconocidosManager` decide si la
/// temática está lista preguntándole a cada NPC si <c>Finished</c>, y eso vivía
/// solo en memoria del objeto de la escena. Con 2 de 3 NPCs hechos, cerrar el juego
/// los devolvía a los tres a cero: había que hacer la temática entera de una
/// sentada o la zona siguiente no se abría nunca. Para un niño/a que juega en ratos
/// cortos, eso podía bloquear el avance del juego, no solo molestar.
///
/// <b>Por qué no se deduce de los chats.</b> La conversación con cada NPC ya se
/// guarda, así que "habló con este NPC" es deducible. Pero el enganche sería por
/// nombre —el acoplamiento por id implícito que ya costó caro tres veces— y sobre
/// todo el chat no guarda el <c>safePercent</c>, así que <c>exito</c> se perdería.
/// Sin él no se puede reconstruir el mapa: el NPC exitoso se retira y el otro no.
///
/// La API es estática por lo mismo que <see cref="ObjetosRecogidosSync"/>: los NPCs
/// consultan y marcan desde su propio código, sin buscar nada en la escena.
/// </summary>
public class NpcTematicaSync : MonoBehaviour
{
    public static NpcTematicaSync Instance { get; private set; }

    private const float EsperaEntreIntentos = 0.5f;
    private const float SegundosAntesDeAvisarQueNoHayPartida = 8f;

    /// <summary>Lo que esta partida ya terminó, y con qué resultado.</summary>
    private static readonly Dictionary<string, bool> terminados = new Dictionary<string, bool>();

    private static bool registroCargado;
    private static int? partidaCargada;
    private bool avisoDeSinPartidaDado;

    /// <summary>Lo marcado sin partida o sin red, para reintentar.</summary>
    private static readonly Dictionary<string, bool> pendientesDeSubir = new Dictionary<string, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarEstadoEstatico()
    {
        // Los estáticos sobreviven al Stop del editor con "Enter Play Mode Options",
        // y sin esto la segunda corrida creería que la temática ya estaba hecha.
        terminados.Clear();
        pendientesDeSubir.Clear();
        registroCargado = false;
        partidaCargada = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        if (Instance != null) return;
        var go = new GameObject("NpcTematicaSync");
        go.AddComponent<NpcTematicaSync>();
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

    // ── Lo que llaman los NPCs ───────────────────────────────────────────────

    /// <summary>Registra que la interacción con ese NPC terminó, y lo sube.</summary>
    public static void Marcar(string npcId, bool exito, string nombreParaElAviso = null)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning(
                $"[NpcTematica] '{nombreParaElAviso ?? "(sin nombre)"}' no tiene npcId, así que " +
                "su avance no se va a recordar y la temática va a empezar de cero la próxima " +
                "vez. Corre Fishy → Asignar ids de escena.");
            return;
        }

        string id = npcId.Trim();
        terminados[id] = exito;
        Subir(id, exito);
    }

    private static void Subir(string id, bool exito)
    {
        var api = ApiManager.Instance;
        if (api == null || api.PartidaId == null)
        {
            pendientesDeSubir[id] = exito;
            return;
        }

        api.MarcarNpcTerminado(id, exito,
            onSuccess: _ => pendientesDeSubir.Remove(id),
            onError: e =>
            {
                pendientesDeSubir[id] = exito;
                Debug.LogWarning($"[NpcTematica] No se pudo guardar '{id}': {e}. Se reintentará.");
            });
    }

    // ── Bajar y repartir ─────────────────────────────────────────────────────

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
                        "[NpcTematica] Llevo varios segundos sin PartidaId: el avance dentro de la " +
                        "temática NO se va a guardar, así que habrá que hacerla entera de una " +
                        "sentada. Si estás probando, entra por MenuUno para pasar por el login.");
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
                    terminados.Clear();
                    Bajar();
                }

                if (pendientesDeSubir.Count > 0)
                    foreach (var par in new Dictionary<string, bool>(pendientesDeSubir))
                        Subir(par.Key, par.Value);

                // Los NPCs viven en la escena de juego, que carga después de que hay
                // partida. Mientras el registro esté cargado se sigue intentando
                // repartirlo: en cuanto la escena aparezca, se aplica.
                if (registroCargado) Repartir();
            }

            yield return espera;
        }
    }

    private void Bajar()
    {
        var api = ApiManager.Instance;
        if (api == null) return;

        api.ObtenerProgresoNpcs(
            onSuccess: lista =>
            {
                if (lista != null)
                    foreach (var n in lista)
                        if (n != null && !string.IsNullOrEmpty(n.npc_id))
                            terminados[n.npc_id] = n.exito;

                registroCargado = true;
                Debug.Log($"[NpcTematica] {terminados.Count} NPC(s) de temática ya terminados.");
                Repartir();
            },
            onError: e =>
            {
                // Sin registro no se restaura nada: la temática empieza de cero, que es
                // como funcionaba antes. Molesto, no destructivo.
                Debug.LogWarning($"[NpcTematica] No se pudo saber el avance de la temática: {e}");
            });
    }

    /// <summary>
    /// Aplica el progreso a los NPCs que estén en la escena y le pide al manager que
    /// recuente. Es idempotente: <c>RestaurarComoTerminado</c> corta si el NPC ya
    /// estaba marcado, así que repetirlo en cada tic no cuesta ni repite efectos.
    /// </summary>
    private static void Repartir()
    {
        if (terminados.Count == 0) return;

        int aplicados = 0;
        foreach (var npc in FindObjectsByType<BosqueDesconocidosNPC>(FindObjectsInactive.Include))
        {
            if (npc == null || npc.Finished) continue;
            if (string.IsNullOrWhiteSpace(npc.npcId)) continue;
            if (!terminados.TryGetValue(npc.npcId.Trim(), out bool exito)) continue;

            npc.RestaurarComoTerminado(exito);
            aplicados++;
        }

        if (aplicados == 0) return;

        Debug.Log($"[NpcTematica] {aplicados} NPC(s) restaurados como terminados.");

        // Recién ahora se pregunta si la temática quedó completa: si el niño/a hizo
        // los tres en sesiones distintas, es aquí donde se abre la zona siguiente.
        if (BosqueDesconocidosManager.Instance != null)
            BosqueDesconocidosManager.Instance.RevisarSiYaEstaCompleta();
    }
}
