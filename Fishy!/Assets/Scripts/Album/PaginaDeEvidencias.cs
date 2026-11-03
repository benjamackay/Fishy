using System.Collections.Generic;
using Fishy.Album;
using Fishy.Detective;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Col = AlbumUITheme.Colores;
using Med = AlbumUITheme.Medidas;
using Fnt = AlbumUITheme.Fuente;
using Txt = AlbumUITheme.Textos;

/// <summary>
/// HDU-12 — El álbum de evidencias: cada señal de riesgo que el niño/a marcó bien en
/// el Modo Detective, agrupada por caso o por temática (CA2), y con su explicación y
/// el contexto del caso al elegirla (CA3).
///
/// Es una pantalla del celular, junto a Misiones e Inventario. Los datos no salen de
/// acá: los guarda <see cref="AlbumDeEvidencias"/>, que es quien sabe acumularlos
/// entre intentos y no borrar nunca lo ganado.
///
/// <b>Los pines ya no están acá.</b> Antes esta pantalla era las dos cosas —el
/// mosaico de pines arriba y las evidencias debajo— y eso obligaba a explicar en la
/// misma pantalla dos cosas que se ganan de forma distinta: el pin se gana superando
/// la mitad de las señales de un caso, y una evidencia se gana marcando UNA bien. Con
/// las dos mezcladas, un álbum con evidencias y sin pines tenía que disculparse por
/// la mitad de su contenido. Ahora los pines viven en
/// <see cref="PaginaDeRecompensas"/>, que es su propia pantalla.
/// </summary>
public class PaginaDeEvidencias : PaginaDeAlbum
{
    /// <summary>Cómo están agrupadas ahora mismo (CA2).</summary>
    private bool _porTematica;

    private Image _pestanaCaso;
    private Image _pestanaTematica;

    protected override float AltoCabeceraPropia => Med.AltoPestanas + 12f;

    protected override void ConstruirCabecera(RectTransform padre)
    {
        var go = new GameObject("Pestanas", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(Med.AnchoPestanas, Med.AltoPestanas);
        rt.anchoredPosition = new Vector2(0f, -MargenPagina);

        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        Button porCaso = FishyUIKit.Boton(go.transform, Txt.PorCaso, Col.Pestana,
            Fnt.Boton, 40f, () => CambiarAgrupacion(false));
        Button porTematica = FishyUIKit.Boton(go.transform, Txt.PorTematica, Col.Pestana,
            Fnt.Boton, 40f, () => CambiarAgrupacion(true));

        _pestanaCaso = porCaso.GetComponent<Image>();
        _pestanaTematica = porTematica.GetComponent<Image>();
        PintarPestanas();
    }

    /// <summary>
    /// Rehace la lista entera desde el estado del álbum.
    ///
    /// Antes de pintar se completa lo que haya quedado a medias: las evidencias que
    /// vinieron del servidor traen solo ids —el backend no guarda el texto del
    /// mensaje ni su explicación— y se rellenan con los casos que viajan empaquetados
    /// en el juego, sin pedirle nada a la red.
    /// </summary>
    protected override void Repoblar()
    {
        if (AlbumDeEvidencias.CantidadIncompletas > 0)
            AlbumDetectivePuente.ConciliarLoQueFalte();

        LimpiarContenido();
        PintarPestanas();

        List<GrupoAlbum> grupos = _porTematica
            ? AlbumDeEvidencias.PorTematica()
            : AlbumDeEvidencias.PorCaso();

        if (grupos.Count == 0)
        {
            // CA4 — Sin evidencias se dice cómo se consiguen, y nada más: el mensaje
            // de "tu álbum está vacío" ya no tiene que hablar también de los pines,
            // que ahora tienen su propia pantalla y su propio mensaje.
            TextoVacio(Txt.VacioEvidencias);
            return;
        }

        foreach (GrupoAlbum grupo in grupos) TarjetaDeGrupo(grupo);
    }

    private void CambiarAgrupacion(bool porTematica)
    {
        if (_porTematica == porTematica) return;

        _porTematica = porTematica;
        CerrarDetalle();
        Repoblar();
    }

    private void PintarPestanas()
    {
        if (_pestanaCaso != null)
            _pestanaCaso.color = _porTematica ? Col.Pestana : Col.PestanaActiva;
        if (_pestanaTematica != null)
            _pestanaTematica.color = _porTematica ? Col.PestanaActiva : Col.Pestana;
    }

    // ── Piezas de la lista ────────────────────────────────────────────────────

    /// <summary>Una tarjeta por grupo: el caso (o la temática), y debajo una fila por
    /// evidencia.</summary>
    private void TarjetaDeGrupo(GrupoAlbum grupo)
    {
        var tarjeta = new GameObject($"Grupo_{grupo.clave}",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        tarjeta.transform.SetParent(Contenido, false);

        FishyUIKit.FondoRedondeado(tarjeta.GetComponent<Image>(), Col.Tarjeta,
            Med.LadoSprite, Med.Radio);

        var vlg = tarjeta.GetComponent<VerticalLayoutGroup>();
        vlg.padding = Med.PaddingTarjeta;
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Agrupando por temática el rótulo viene con la etiqueta cruda del banco
        // ("retos_virales"): el núcleo del álbum agrupa, pero no puede nombrar —el
        // diccionario de nombres vive de este lado—. Ver GrupoAlbum.
        string titulo = _porTematica ? Txt.NombreTematica(grupo.clave) : grupo.titulo;

        TextMeshProUGUI tituloTmp = FishyUIKit.Texto(tarjeta.transform, "Titulo", titulo,
            Fnt.TituloGrupo, Col.Texto, TextAlignmentOptions.Left);
        tituloTmp.font = Fnt.Titulo;
        tituloTmp.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.TituloGrupo * 1.3f;

        if (!string.IsNullOrWhiteSpace(grupo.subtitulo))
        {
            TextMeshProUGUI sub = FishyUIKit.Texto(tarjeta.transform, "Subtitulo", grupo.subtitulo,
                Fnt.Subtitulo, Col.TextoSuave, TextAlignmentOptions.Left);
            sub.font = Fnt.Cuerpo;
            sub.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.Subtitulo * 1.3f;
        }

        foreach (EvidenciaAlbum evidencia in grupo.evidencias)
            FilaDeEvidencia(tarjeta.transform, evidencia);
    }

    private void FilaDeEvidencia(Transform padre, EvidenciaAlbum evidencia)
    {
        var fila = new GameObject($"Evidencia_{evidencia.mensajeId}",
            typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement), typeof(VerticalLayoutGroup));
        fila.transform.SetParent(padre, false);

        FishyUIKit.FondoRedondeado(fila.GetComponent<Image>(), Col.Fila, Med.LadoSprite, Med.Radio);
        fila.GetComponent<LayoutElement>().minHeight = Med.AlturaFila;

        // El grupo mide el texto y le pide ese alto a la tarjeta: una señal es una
        // frase entera y a este ancho envuelve a dos líneas. Con alto fijo se
        // desbordaba por encima de la fila siguiente.
        var vlg = fila.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        TextMeshProUGUI t = FishyUIKit.Texto(fila.transform, "Texto", ResumenDe(evidencia),
            Fnt.Fila, Col.Texto, TextAlignmentOptions.Left);
        t.font = Fnt.Cuerpo;
        t.raycastTarget = false;

        fila.GetComponent<Button>().onClick.AddListener(() => MostrarEvidencia(evidencia));
    }

    /// <summary>Lo que se lee en la fila. De una evidencia que todavía es un esqueleto
    /// —vino del servidor y no se pudo completar— se enseña el id del mensaje: es
    /// poco, pero deja ver que la evidencia está ahí.</summary>
    private static string ResumenDe(EvidenciaAlbum evidencia)
    {
        if (!evidencia.EstaCompleta) return evidencia.mensajeId;

        string texto = Recortar(evidencia.texto, 90);
        return string.IsNullOrWhiteSpace(evidencia.autor) ? texto : $"{evidencia.autor}: {texto}";
    }

    private static string Recortar(string texto, int largo)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length <= largo) return texto;
        return texto.Substring(0, largo).TrimEnd() + "…";
    }

    // ── Detalle (CA3) ─────────────────────────────────────────────────────────

    /// <summary>
    /// CA3 — El detalle de una evidencia: la explicación de por qué ese mensaje era
    /// una señal, y el contexto del caso (de qué caso es, en qué zona pasó, quién lo
    /// escribió y qué decía).
    /// </summary>
    private void MostrarEvidencia(EvidenciaAlbum evidencia)
    {
        string subtitulo = evidencia.casoZona;
        if (!string.IsNullOrWhiteSpace(evidencia.autor))
            subtitulo = string.IsNullOrWhiteSpace(subtitulo)
                ? $"Lo escribió {evidencia.autor}"
                : $"{evidencia.casoZona} · lo escribió {evidencia.autor}";

        if (!evidencia.EstaCompleta)
        {
            MostrarTarjeta(evidencia.TituloVisible, subtitulo, evidencia.etiquetas,
                null, Txt.EvidenciaSinDatos);
            return;
        }

        MostrarTarjeta(evidencia.TituloVisible, subtitulo, evidencia.etiquetas,
            $"«{evidencia.texto}»", evidencia.explicacion);
    }
}
