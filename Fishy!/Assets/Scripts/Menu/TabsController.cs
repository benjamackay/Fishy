using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pestañas del menú del Tab. Empareja cada pestaña con su página y deja visible
/// una sola a la vez.
///
/// El emparejamiento se deduce de la jerarquía y del nombre —"QuestTab" abre
/// "QuestPage"—, así que no hay que llenar listas a mano ni repetir el índice de
/// cada pestaña en un EventTrigger. Los arrays quedan igual por si hace falta
/// forzar un caso raro: si vienen llenos y con el mismo largo, mandan ellos.
/// </summary>
public class TabsController : MonoBehaviour
{
    [Header("Cableado (opcional)")]
    [Tooltip("Si se dejan vacíos se descubren solos: las pestañas son los hijos de este objeto " +
             "y las páginas los hijos de 'Pages', emparejadas por nombre.")]
    public UnityEngine.UI.Image[] tabImages;
    public GameObject[] pages;

    [Tooltip("Contenedor de las páginas. Si queda vacío se busca un hijo o un hermano llamado 'Pages'.")]
    public Transform pagesRoot;

    private const string SufijoPestana = "Tab";
    private const string SufijoPagina = "Page";
    private const string NombreContenedorPaginas = "Pages";

    private void Awake()
    {
        if (HayCableadoManual()) ValidarEmparejamiento();
        else DescubrirPorJerarquia();

        CablearClicks();
        AsegurarControladoresDePaginas();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ActivateTab(0);
    }

    /// <summary>
    /// El Inspector manda solo si trae ambas listas completas y del mismo largo:
    /// un cableado a medias es justo el caso que rompía las pestañas en silencio.
    /// </summary>
    private bool HayCableadoManual()
    {
        return tabImages != null && pages != null
            && tabImages.Length > 0
            && tabImages.Length == pages.Length;
    }

    /// <summary>
    /// Con las listas puestas a mano, comprueba contra el nombre que cada pestaña
    /// esté frente a su página. Es exactamente el fallo que antes pasaba mudo:
    /// hacer clic en "Misión" y que se abriera Inventario porque alguien reordenó
    /// un array o duplicó una pestaña.
    /// </summary>
    private void ValidarEmparejamiento()
    {
        for (int i = 0; i < tabImages.Length && i < pages.Length; i++)
        {
            if (tabImages[i] == null || pages[i] == null)
            {
                Debug.LogError($"[Tabs] La posición {i} tiene una casilla vacía en el Inspector.", this);
                continue;
            }

            string basePestana = QuitarSufijo(tabImages[i].name, SufijoPestana);
            string basePagina = QuitarSufijo(pages[i].name, SufijoPagina);

            if (basePestana != basePagina)
            {
                Debug.LogError($"[Tabs] Desalineado en la posición {i}: la pestaña " +
                               $"'{tabImages[i].name}' está abriendo la página '{pages[i].name}'. " +
                               "Vacía ambas listas para que se emparejen solas por nombre.", this);
            }
        }
    }

    /// <summary>
    /// Recorre los hijos en orden y busca para cada pestaña la página que le toca.
    /// Una pestaña sin página se conserva en la lista con su hueco en null: sacarla
    /// correría los índices y haría que las demás abrieran la página equivocada.
    /// </summary>
    private void DescubrirPorJerarquia()
    {
        Transform raizPaginas = ResolverRaizDePaginas();
        if (raizPaginas == null)
        {
            Debug.LogError($"[Tabs] No encuentro el contenedor de páginas ('{NombreContenedorPaginas}'). " +
                           "Asigna 'Pages Root' en el Inspector.", this);
            tabImages = new Image[0];
            pages = new GameObject[0];
            return;
        }

        var imagenes = new List<Image>();
        var paginas = new List<GameObject>();

        foreach (Transform hijo in transform)
        {
            var imagen = hijo.GetComponent<Image>();
            if (imagen == null) continue;   // no es una pestaña, es decoración

            string baseNombre = QuitarSufijo(hijo.name, SufijoPestana);
            Transform pagina = raizPaginas.Find(baseNombre + SufijoPagina);

            if (pagina == null)
            {
                Debug.LogError($"[Tabs] La pestaña '{hijo.name}' esperaba una página " +
                               $"'{baseNombre}{SufijoPagina}' dentro de '{raizPaginas.name}' y no está. " +
                               "Revisa el nombre o asigna las listas a mano.", hijo);
            }

            imagenes.Add(imagen);
            paginas.Add(pagina != null ? pagina.gameObject : null);
        }

        tabImages = imagenes.ToArray();
        pages = paginas.ToArray();
    }

    private Transform ResolverRaizDePaginas()
    {
        if (pagesRoot != null) return pagesRoot;

        Transform comoHijo = transform.Find(NombreContenedorPaginas);
        if (comoHijo != null) return comoHijo;

        return transform.parent != null
            ? transform.parent.Find(NombreContenedorPaginas)
            : null;
    }

    private static string QuitarSufijo(string nombre, string sufijo)
    {
        return nombre.EndsWith(sufijo)
            ? nombre.Substring(0, nombre.Length - sufijo.Length)
            : nombre;
    }

    /// <summary>
    /// Deja cada pestaña abriendo su propia página. Con Button en vez del
    /// EventTrigger de la escena se gana navegación por teclado; la transición
    /// se apaga porque el color lo maneja <see cref="ActivateTab"/> y si no
    /// ambos pelearían por el mismo Image.
    /// </summary>
    private void CablearClicks()
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            if (tabImages[i] == null) continue;

            int indice = i;   // copia local: sin esto todas las pestañas abrirían la última

            var boton = tabImages[i].GetComponent<Button>();
            if (boton == null) boton = tabImages[i].gameObject.AddComponent<Button>();

            boton.targetGraphic = tabImages[i];
            boton.transition = Selectable.Transition.None;
            boton.onClick.RemoveAllListeners();
            boton.onClick.AddListener(() => ActivateTab(indice));
        }
    }

    /// <summary>
    /// Las páginas del Tab ya están asignadas en la escena. Conecta sus
    /// controladores automáticamente para que no dependan de que el componente
    /// se haya agregado a mano y guardado en el archivo de escena.
    /// </summary>
    private void AsegurarControladoresDePaginas()
    {
        if (pages == null) return;

        foreach (GameObject page in pages)
        {
            if (page == null) continue;

            if (page.name == "QuestPage" && page.GetComponent<QuestPageUI>() == null)
                page.AddComponent<QuestPageUI>();
        }
    }

    public void ActivateTab(int tabNo)
    {
        if (pages == null || pages.Length == 0) return;

        if (tabNo < 0 || tabNo >= pages.Length)
        {
            Debug.LogError($"[Tabs] ActivateTab({tabNo}) fuera de rango: hay {pages.Length} pestañas.", this);
            return;
        }

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null) pages[i].SetActive(i == tabNo);

            if (i < tabImages.Length && tabImages[i] != null)
                tabImages[i].color = (i == tabNo) ? Color.white : Color.grey;
        }
    }
}
