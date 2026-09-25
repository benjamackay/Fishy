using System.Collections.Generic;
using Fishy.Mision;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Los marcadores del mapa del celular:
/// <list type="bullet">
///   <item><b>«!» azul</b> — un NPC o un chat de teléfono que hay que atender.</item>
///   <item><b>«?» morado</b> — un caso del Modo Detective por resolver.</item>
///   <item><b>Estrella amarilla</b> — un objeto del mapa que todavía no se ha recogido.</item>
/// </list>
///
/// Los dos primeros son exactamente los mismos objetivos que señala <see cref="NpcMarker"/> en el
/// mundo (misión activa, objetivo pendiente, con NPC ya resuelto), y comparten con él la regla y
/// los dibujos, así que mapa y mundo cuentan lo mismo.
///
/// Van colgados del propio mapa: se desplazan con él y su posición se calcula con
/// <see cref="MapPageUI.PosicionEnMapa"/>. Se vuelven a crear cada vez que se abre la página:
/// con el panel abierto Otto está quieto, así que nada cambia mientras se mira.
/// </summary>
public class MapaMarcadoresUI : MonoBehaviour
{
    private MapPageUI _pagina;

    public static MapaMarcadoresUI Crear(MapPageUI pagina, RectTransform mapa)
    {
        var go = new GameObject("Marcadores", typeof(RectTransform));
        go.transform.SetParent(mapa, false);

        // Mismo origen que el mapa (su centro): la posición de cada marcador es directamente
        // la que devuelve PosicionEnMapa.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var marcadores = go.AddComponent<MapaMarcadoresUI>();
        marcadores._pagina = pagina;
        return marcadores;
    }

    /// <summary>Borra y vuelve a colocar todos los marcadores. Hay que llamarlo cuando el mapa
    /// ya tiene su tamaño, porque de él depende dónde cae cada uno.</summary>
    public void Refrescar()
    {
        if (_pagina == null || _pagina.Mapa == null || _pagina.Mapa.rect.width < 1f) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // Primero las estrellas y luego los signos, para que un «!» nunca quede tapado por una.
        PonerEstrellas();
        PonerSignos();
    }

    // ── Objetos por recoger ──────────────────────────────────────────────────

    private void PonerEstrellas()
    {
        Sprite dibujo = DibujoMarcadores.Estrella;

        // Los WorldItem ya recogidos se destruyen o se apagan, y CanInteract() cubre el caso
        // de uno que está pero ya no se puede recoger. Los de zonas todavía cerradas suelen
        // estar apagados y no se ven: el mapa no adelanta lo que aún no se ha abierto.
        foreach (WorldItem objeto in FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
        {
            if (objeto == null || !objeto.CanInteract()) continue;

            float t = MapaTheme.Medidas.TamanoEstrella;
            Vector2 pos = _pagina.PosicionEnMapa(objeto.transform.position);
            Imagen("Estrella_" + objeto.name, dibujo, pos, new Vector2(t, t), new Vector2(0.5f, 0.5f));
        }
    }

    // ── NPC, chats y casos por atender ───────────────────────────────────────

    private void PonerSignos()
    {
        DesafioRuntime activa = MissionManager.GetOrCreate().Activa;
        if (activa == null || MissionTracker.Instance == null) return;

        var yaPuestos = new HashSet<Component>();

        foreach (ObjetivoMision objetivo in MissionTracker.Instance.Objetivos(activa.Id))
        {
            if (objetivo == null || objetivo.cumplido || !NpcMarker.EsDeInteraccion(objetivo.tipo)) continue;

            // Un NPC de una zona que aún no se cargó no existe todavía: no se marca. Cuando
            // exista, la próxima vez que se abra la página aparecerá.
            Component destino = NpcMarker.DestinoDe(objetivo);
            if (destino == null || !destino.gameObject.activeInHierarchy || !yaPuestos.Add(destino)) continue;

            bool esCaso = objetivo.tipo == TipoObjetivo.CompletarCasoDetective;
            Sprite dibujo = esCaso ? DibujoMarcadores.Interrogacion : DibujoMarcadores.Exclamacion;
            string signo = esCaso ? "?" : "!";
            Color color = esCaso ? MisionHudTheme.Colores.ObjetivoDetective : MisionHudTheme.Colores.ObjetivoNpc;

            // El pie del signo queda sobre el punto: señala el sitio, en vez de taparlo.
            Vector2 pos = _pagina.PosicionEnMapa(destino.transform.position);
            Vector2 pie = new Vector2(0.5f, 0f);
            float alto = MapaTheme.Medidas.AlturaSigno;

            if (dibujo != null)
            {
                float ancho = alto * dibujo.rect.width / Mathf.Max(1f, dibujo.rect.height);
                Imagen("Signo_" + destino.name, dibujo, pos, new Vector2(ancho, alto), pie);
            }
            else
            {
                TextoDeRespaldo("Signo_" + destino.name, signo, color, pos, alto, pie);
            }
        }
    }

    // ── Piezas ───────────────────────────────────────────────────────────────

    private void Imagen(string nombre, Sprite dibujo, Vector2 pos, Vector2 tamano, Vector2 pivote)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivote;
        rt.sizeDelta = tamano;
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = dibujo;
        img.raycastTarget = false;
    }

    /// <summary>El signo escrito con la fuente, para cuando todavía no hay PNG.</summary>
    private void TextoDeRespaldo(string nombre, string signo, Color color, Vector2 pos, float alto, Vector2 pivote)
    {
        TextMeshProUGUI t = FishyUIKit.Texto(transform, nombre, signo, alto, color, TextAlignmentOptions.Center);
        t.font = FishyUIKit.FuentePara(signo);
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;
        t.outlineColor = MisionHudTheme.Colores.FlechaBorde;

        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivote;
        rt.sizeDelta = new Vector2(alto, alto);
        rt.anchoredPosition = pos;
    }
}
