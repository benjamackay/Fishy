using System.Collections.Generic;
using Fishy.Mision;
using Fishy.World;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cuánto se lleva hecho de una zona: misiones completadas y objetos recogidos, sobre el total
/// que tiene esa zona. Lo enseña la tarjeta del mapa (<see cref="MapaTarjetaUI"/>) como un
/// porcentaje.
///
/// <b>Los totales tienen que ser fijos</b>, o el porcentaje miente: con un total que crece a
/// medida que se juega, completar la primera misión daría «100 %». Por eso:
/// <list type="bullet">
///   <item><b>Misiones:</b> el total sale del catálogo (<see cref="CatalogoMisiones"/>), que trae
///   todas —también las que aún no se han entregado— con su zona de destino. Sólo cuentan las
///   que declaran una (<c>zona_objetivo</c>); una misión sin zona no pertenece a ninguna.</item>
///   <item><b>Objetos:</b> el total son todos los <see cref="WorldItem"/> que hubo en la escena,
///   recogidos o no. Un objeto recogido desaparece del mapa, así que hay que anotarlos apenas
///   se carga la escena, ANTES de que cada objeto se retire en su <c>Start</c>; por eso el
///   registro se llena al evento <c>sceneLoaded</c> (que corre después de Awake y antes de Start).</item>
/// </list>
///
/// Un objeto cuenta como recogido si ya no existe, si ya no se puede recoger o si el registro de
/// recogidos lo tiene: los tres caminos por los que <see cref="WorldItem"/> se da por recogido,
/// sin depender de que todos los objetos tengan su <c>objetoId</c> asignado.
/// </summary>
public static class ProgresoDeZona
{
    /// <summary>El resultado para una zona.</summary>
    public readonly struct Resultado
    {
        public readonly int MisionesHechas, MisionesTotales;
        public readonly int ObjetosHechos, ObjetosTotales;

        public Resultado(int mh, int mt, int oh, int ot)
        {
            MisionesHechas = mh; MisionesTotales = mt;
            ObjetosHechos = oh;  ObjetosTotales = ot;
        }

        public int Hechos => MisionesHechas + ObjetosHechos;
        public int Totales => MisionesTotales + ObjetosTotales;

        /// <summary>La zona no tiene nada que hacer (ni misiones ni objetos): no hay porcentaje.</summary>
        public bool HayDatos => Totales > 0;

        /// <summary>0 a 100, redondeado. Sin datos devuelve 0; comprobar <see cref="HayDatos"/>.</summary>
        public int Porcentaje => Totales > 0 ? Mathf.RoundToInt(100f * Hechos / Totales) : 0;
    }

    private class Objeto
    {
        public WorldItem referencia;
        public string id;
        public Vector2 posicion;   // se guarda: cuando el objeto se destruye ya no se puede preguntar
    }

    private static readonly List<Objeto> _objetos = new List<Objeto>();

    // ── Registro de objetos ──────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Suscribir()
    {
        // Quitar antes de poner: sin recarga de dominio al entrar en Play la suscripción de la
        // sesión anterior sigue viva y se registrarían los objetos dos veces.
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        // Cada carga de escena empieza de cero: los objetos de la escena anterior ya no existen.
        if (modo == LoadSceneMode.Single) _objetos.Clear();
        Anotar();
    }

    /// <summary>Anota los WorldItem de la escena que todavía no estén en el registro. Se mira
    /// también los apagados: los de zonas aún cerradas suelen estarlo, y son parte del total.</summary>
    private static void Anotar()
    {
        foreach (WorldItem item in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include))
        {
            if (item == null || YaAnotado(item)) continue;

            _objetos.Add(new Objeto
            {
                referencia = item,
                id = item.objetoId,
                posicion = item.transform.position,
            });
        }
    }

    private static bool YaAnotado(WorldItem item)
    {
        foreach (Objeto o in _objetos)
            if (o.referencia == item) return true;
        return false;
    }

    // ── Cálculo ──────────────────────────────────────────────────────────────

    /// <summary>El progreso de esa zona (por su id: zona_1, zona_2…).</summary>
    public static Resultado De(string zona)
    {
        if (string.IsNullOrWhiteSpace(zona)) return default;
        zona = zona.Trim();

        // Por si apareció algún objeto después de cargar la escena (uno instanciado a mano).
        Anotar();

        int misionesHechas = 0, misionesTotales = 0;
        MissionManager manager = MissionManager.Instance;
        foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
        {
            if (m == null || string.IsNullOrWhiteSpace(m.zona_objetivo)) continue;
            if (m.zona_objetivo.Trim() != zona) continue;

            misionesTotales++;
            if (manager != null && manager.EstaCompletado((m.mision_id ?? "").Trim())) misionesHechas++;
        }

        int objetosHechos = 0, objetosTotales = 0;
        ZonaActual zonas = ZonaActual.Instance;
        if (zonas != null)
        {
            foreach (Objeto o in _objetos)
            {
                if (zonas.ZonaEn(o.posicion) != zona) continue;

                objetosTotales++;
                if (EstaRecogido(o)) objetosHechos++;
            }
        }

        return new Resultado(misionesHechas, misionesTotales, objetosHechos, objetosTotales);
    }

    private static bool EstaRecogido(Objeto o)
    {
        // Destruido al recogerlo (destroyOnPickup) o apagado por haber sido recogido.
        if (o.referencia == null || !o.referencia.CanInteract()) return true;

        return !string.IsNullOrEmpty(o.id) && ObjetosRecogidosSync.YaFueRecogido(o.id);
    }
}
