using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La tarjeta de la página "Mapa": en qué zona está Otto y cuánto lleva hecho de ella.
///
/// La monta <see cref="MapPageUI"/> sobre el mapa, arriba a la izquierda. No decide nada: lee lo
/// que ya saben <see cref="ZonaActual"/> y <see cref="ProgresoDeZona"/>.
///
/// <b>Dos mitades.</b> Arriba, «Zona actual» y su nombre. Abajo, «Progreso» y un solo porcentaje;
/// esa mitad empieza justo en el centro de la tarjeta, así que el título queda a la altura del
/// medio sin importar cuántas líneas ocupe el nombre de la zona.
///
/// <b>Qué mide el porcentaje:</b> misiones completadas y objetos recogidos de la zona donde está
/// Otto, sobre el total de esa zona. Ver <see cref="ProgresoDeZona"/>.
///
/// Se pinta al abrirse la página. Con el panel abierto Otto está quieto, así que nada cambia
/// mientras se mira.
/// </summary>
public class MapaTarjetaUI : MonoBehaviour
{
    private TextMeshProUGUI _nombreZona;
    private TextMeshProUGUI _numeroProgreso;

    /// <summary>Crea la tarjeta como hija de <paramref name="padre"/> (el visor del mapa).</summary>
    public static MapaTarjetaUI Crear(RectTransform padre)
    {
        var go = new GameObject("TarjetaMapa", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);

        var tarjeta = go.AddComponent<MapaTarjetaUI>();
        tarjeta.Construir();
        return tarjeta;
    }

    private void OnEnable() => Pintar();

    // ── Construcción ─────────────────────────────────────────────────────────

    private void Construir()
    {
        float marco = MapaTheme.Medidas.GrosorMarco;

        // Marco: el propio objeto, en el color del borde; el relleno va dentro, más pequeño.
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);   // esquina superior izquierda
        rt.sizeDelta = new Vector2(MapaTheme.Medidas.AnchoTarjeta, MapaTheme.Medidas.AltoTarjeta);
        rt.anchoredPosition = new Vector2(MapaTheme.Medidas.MargenIzquierdo, -MapaTheme.Medidas.MargenSuperior);

        var borde = GetComponent<Image>();
        FishyUIKit.FondoRedondeado(borde, MapaTheme.Colores.BordeTarjeta, 96,
            Mathf.RoundToInt(MapaTheme.Medidas.RadioEsquinas));
        borde.raycastTarget = false;

        var relleno = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
        relleno.transform.SetParent(transform, false);
        var rrt = (RectTransform)relleno.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = new Vector2(marco, marco);
        rrt.offsetMax = new Vector2(-marco, -marco);
        var img = relleno.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, MapaTheme.Colores.FondoTarjeta, 96,
            Mathf.Max(1, Mathf.RoundToInt(MapaTheme.Medidas.RadioEsquinas - marco)));
        img.raycastTarget = false;

        // Mitad de arriba: de la mitad de la tarjeta hacia arriba.
        Transform arriba = Mitad("Zona", relleno.transform, 0.5f, 1f, TextAnchor.UpperCenter);
        Texto(arriba, "TituloZona", MapaTheme.Textos.ZonaActual,
              MapaTheme.Fuente.Titulo, MapaTheme.Colores.Titulo, TextAlignmentOptions.TopLeft);
        _nombreZona = Texto(arriba, "NombreZona", "",
              MapaTheme.Fuente.NombreZona, MapaTheme.Colores.NombreZona, TextAlignmentOptions.Top);

        // Mitad de abajo: de la mitad de la tarjeta hacia abajo. Su título arranca en el centro.
        Transform abajo = Mitad("Progreso", relleno.transform, 0f, 0.5f, TextAnchor.UpperCenter);
        Texto(abajo, "TituloProgreso", MapaTheme.Textos.Progreso,
              MapaTheme.Fuente.Titulo, MapaTheme.Colores.Titulo, TextAlignmentOptions.TopLeft);
        _numeroProgreso = Texto(abajo, "NumeroProgreso", "0%",
              MapaTheme.Fuente.NumeroProgreso, MapaTheme.Colores.NumeroProgreso, TextAlignmentOptions.Top);
    }

    /// <summary>Una mitad de la tarjeta: una columna que crece hacia abajo desde su borde de arriba.</summary>
    private static Transform Mitad(string nombre, Transform padre, float desde, float hasta, TextAnchor alineacion)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, desde);
        rt.anchorMax = new Vector2(1f, hasta);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(MapaTheme.Medidas.RellenoLateral, MapaTheme.Medidas.RellenoLateral,
                                     MapaTheme.Medidas.RellenoArriba, MapaTheme.Medidas.RellenoAbajo);
        vlg.spacing = MapaTheme.Medidas.SeparacionEntreLineas;
        vlg.childAlignment = alineacion;
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        return go.transform;
    }

    private static TextMeshProUGUI Texto(Transform padre, string nombre, string texto, float tamano,
                                         Color color, TextAlignmentOptions alineacion)
    {
        TextMeshProUGUI t = FishyUIKit.Texto(padre, nombre, texto, tamano, color, alineacion);
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    // ── Contenido ────────────────────────────────────────────────────────────

    private void Pintar()
    {
        if (_nombreZona == null) return;

        string zona = IdDeLaZonaActual();
        PonerTexto(_nombreZona, ZonaMundo.NombreDe(zona));

        ProgresoDeZona.Resultado progreso = ProgresoDeZona.De(zona);
        _numeroProgreso.color = progreso.HayDatos
            ? MapaTheme.Colores.NumeroProgreso
            : MapaTheme.Colores.NumeroProgresoVacio;
        PonerTexto(_numeroProgreso, progreso.HayDatos ? progreso.Porcentaje + "%" : MapaTheme.Textos.SinDatos);

        // El alto de cada texto depende de lo que diga: se recalcula el diseño ya, no al
        // final del frame, para que no se vea un instante con las medidas del texto anterior.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_nombreZona.transform.parent);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_numeroProgreso.transform.parent);
    }

    private static string IdDeLaZonaActual()
    {
        ZonaActual zonas = ZonaActual.Instance;
        if (zonas == null) return "";
        return string.IsNullOrEmpty(zonas.Actual) ? zonas.zonaPorDefecto : zonas.Actual;
    }

    /// <summary>La fuente se decide con el texto puesto: Mango no trae todos los caracteres.</summary>
    private static void PonerTexto(TextMeshProUGUI t, string texto)
    {
        t.font = FishyUIKit.FuentePara(texto);
        t.text = texto;
    }
}
