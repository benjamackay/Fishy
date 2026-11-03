using System.Collections.Generic;
using Fishy.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Col = AlbumUITheme.Colores;
using Med = AlbumUITheme.Medidas;
using Txt = AlbumUITheme.Textos;

/// <summary>
/// HDU-11 — El álbum de recompensas: los pines que el niño/a se ganó superando casos
/// del Modo Detective, en mosaico.
///
/// Es la otra pantalla del celular, hermana de <see cref="PaginaDeEvidencias"/>. Son
/// dos álbumes y no uno porque se ganan de forma distinta y conviene que se entienda:
/// un pin pide acertar más de la mitad de las señales de un caso; una evidencia la
/// deja UNA señal bien marcada. Mezclados en una sola pantalla, cada estado vacío
/// tenía que disculparse por la mitad del contenido.
///
/// Los pines no los guarda el álbum: son ítems de la mochila cuyo id empieza por
/// <see cref="AlbumEvidenciasUI.PrefijoPin"/>, y quien los entrega es
/// <c>DetectiveCaseManager</c>. Acá solo se miran.
/// </summary>
public class PaginaDeRecompensas : PaginaDeAlbum
{
    protected override string PistaPorDefecto => Txt.PistaDefecto;

    protected override void Repoblar()
    {
        LimpiarContenido();
        MostrarPista(Txt.PistaDefecto);

        List<Item> pines = PinesDeLaMochila();

        if (pines.Count == 0)
        {
            TextoVacio(Txt.SinPines);
            return;
        }

        MosaicoDePines(pines);
    }

    private static List<Item> PinesDeLaMochila()
    {
        var pines = new List<Item>();
        if (InventoryManager.Instance == null) return pines;

        foreach (Item item in InventoryManager.Instance.inventory)
        {
            if (item.itemData != null && !string.IsNullOrEmpty(item.itemData.itemId) &&
                item.itemData.itemId.StartsWith(AlbumEvidenciasUI.PrefijoPin))
                pines.Add(item);
        }
        return pines;
    }

    /// <summary>El mosaico: solo el ícono. El nombre y la descripción no entran en la
    /// casilla sin desbordarse, así que se leen en la franja del pie al pasar el
    /// mouse, o en el detalle al pulsar — que es la única forma de consultarlos en
    /// una pantalla táctil.</summary>
    private void MosaicoDePines(List<Item> pines)
    {
        var go = new GameObject("Pines", typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(Contenido, false);

        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(Med.LadoPin, Med.LadoPin);
        grid.spacing = new Vector2(Med.EspacioPin, Med.EspacioPin);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Med.ColumnasPines;

        foreach (Item item in pines) CasillaDePin(go.transform, item);
    }

    private void CasillaDePin(Transform padre, Item item)
    {
        var casilla = new GameObject("Pin", typeof(RectTransform), typeof(Image), typeof(Button));
        casilla.transform.SetParent(padre, false);

        var fondo = casilla.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(fondo, Col.RanuraPin, Med.LadoSprite, Med.Radio);

        var iconoGO = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        iconoGO.transform.SetParent(casilla.transform, false);
        FishyUIKit.Estirar((RectTransform)iconoGO.transform, 8f, 8f);
        var icono = iconoGO.GetComponent<Image>();
        icono.sprite = item.itemData.itemIcon;
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        // Sin sprite se deja un cuadro tenue en vez de nada: así se ve que hay un pin
        // ahí —y se puede seguir pasando el mouse o pulsarlo para saber cuál es—
        // aunque el ItemData todavía no tenga el ícono asignado.
        icono.color = item.itemData.itemIcon != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);

        string nombre = item.itemData.itemName;
        string descripcion = item.itemData.itemDescription;

        string pista = nombre;
        if (!string.IsNullOrEmpty(descripcion)) pista += $" — {descripcion}";

        var hover = casilla.AddComponent<PinHover>();
        hover.pagina = this;
        hover.texto = pista;

        casilla.GetComponent<Button>().onClick.AddListener(
            () => MostrarTarjeta(nombre, Txt.SeccionPines, null, null, descripcion));
    }

    /// <summary>Enganche de mouse de una casilla. Clase aparte (y no la página
    /// entera) porque sólo el ícono necesita reaccionar al hover.</summary>
    private class PinHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public PaginaDeRecompensas pagina;
        public string texto;

        public void OnPointerEnter(PointerEventData eventData) => pagina.MostrarPista(texto);
        public void OnPointerExit(PointerEventData eventData) => pagina.MostrarPista(Txt.PistaDefecto);
    }
}
