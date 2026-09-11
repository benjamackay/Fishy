using System.Collections.Generic;
using Fishy.Mision;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta las misiones del <see cref="MissionManager"/> dentro de la página
/// "QuestPage" del panel del Tab.
///
/// Va en el GameObject "QuestPage". Es el equivalente de
/// <see cref="InventoryManagerUI"/> para misiones: <b>la lista completa</b>, con
/// las ya terminadas incluidas, para cuando el niño/a quiere repasar.
///
/// Convive con <see cref="MissionUIController"/> (HDU-16), el cartel permanente
/// que enseña una sola misión —la activa— sin abrir nada. Los dos leen el mismo
/// MissionManager, así que no pueden contradecirse; para que además se note cuál
/// de las de esta lista es la del cartel, la activa se marca con "ACTIVA".
///
/// Como el resto de páginas, se refresca al abrirse y ante cualquier cambio,
/// así que una misión entregada con el panel abierto aparece al instante.
/// </summary>
public class QuestPageUI : MonoBehaviour
{
    [Header("Montaje (opcional)")]
    [Tooltip("Dónde se cuelgan las filas. Si se deja vacío se crea una lista aquí mismo.")]
    public Transform questContainer;
    [Tooltip("Fila de ejemplo del diseño. Se mantiene oculta y se clona para cada misión.")]
    public GameObject rowTemplate;

    [Header("Aspecto")]
    public Color colorDisponible = MenuTabsTheme.Colores.MisionEnCurso;
    public Color colorCompletado = MenuTabsTheme.Colores.MisionCompletada;
    public float tituloFontSize = MenuTabsTheme.Fuente.TituloMision;
    public float objetivoFontSize = MenuTabsTheme.Fuente.Objetivo;

    [Tooltip("Qué decir cuando no hay ninguna misión. Vacío = no mostrar nada.")]
    public string emptyMessage = MenuTabsTheme.Textos.SinMisiones;

    [Header("Diagnóstico")]
    [Tooltip("Escribir en consola cada refresco. Si abres la pestaña y no aparece nada " +
             "en el Console, este componente no está montado.")]
    public bool verboseLogs = true;

    private readonly List<GameObject> filas = new List<GameObject>();
    private MissionTracker trackerSuscrito;
    private InventoryManager inventarioSuscrito;

    private void Awake()
    {
        if (questContainer == null) questContainer = CrearContenedor();
        if (rowTemplate != null) rowTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        MissionManager manager = MissionManager.GetOrCreate();
        if (manager.onPanelActualizado == null)
            manager.onPanelActualizado = new UnityEngine.Events.UnityEvent();

        manager.onPanelActualizado.AddListener(Refresh);

        // Avanzar un objetivo no cambia la lista de misiones, así que el MissionManager
        // no avisa: sin esto un objetivo cumplido con la página abierta no se ponía
        // verde hasta volver a entrar. El inventario, por los "Juntar X (1/3)", que
        // suben de a uno sin llegar a cumplir el objetivo.
        trackerSuscrito = MissionTracker.Instance;
        if (trackerSuscrito != null) trackerSuscrito.OnProgresoCambiado += Refresh;
        inventarioSuscrito = InventoryManager.Instance;
        if (inventarioSuscrito != null) inventarioSuscrito.OnInventoryChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.onPanelActualizado?.RemoveListener(Refresh);

        if (trackerSuscrito != null) trackerSuscrito.OnProgresoCambiado -= Refresh;
        trackerSuscrito = null;
        if (inventarioSuscrito != null) inventarioSuscrito.OnInventoryChanged -= Refresh;
        inventarioSuscrito = null;
    }

    /// <summary>Reconstruye la lista desde el estado actual del MissionManager.</summary>
    public void Refresh()
    {
        foreach (GameObject fila in filas)
        {
            if (fila == null) continue;
            // Apagarla antes de destruirla: Destroy es diferido y si no la fila vieja
            // seguiría ocupando sitio en el layout durante este frame.
            fila.SetActive(false);
            Destroy(fila);
        }
        filas.Clear();

        List<DesafioRuntime> misiones = MissionManager.GetOrCreate().GetListaOrdenada();
        foreach (DesafioRuntime mision in misiones) filas.Add(ConstruirFila(mision));

        if (misiones.Count == 0 && !string.IsNullOrEmpty(emptyMessage))
        {
            if (rowTemplate != null) filas.Add(TemplateRow("Sin misiones", emptyMessage));
            else filas.Add(ConstruirTexto(emptyMessage, tituloFontSize,
                MenuTabsTheme.Colores.TextoVacio, questContainer, MenuTabsTheme.Fuente.Titulo));
        }

        if (verboseLogs)
            Debug.Log($"[Misiones] Página refrescada: {misiones.Count} misión(es).", this);
    }

    // ── Construcción de filas ────────────────────────────────────────────────

    private GameObject ConstruirFila(DesafioRuntime mision)
    {
        if (rowTemplate != null) return MissionTemplateRow(mision);
        bool completada = mision.estado == EstadoDesafio.Completado;

        var filaGO = new GameObject($"Mision_{mision.Id}",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        filaGO.transform.SetParent(questContainer, false);

        var layout = filaGO.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        string progreso = MissionTracker.Instance != null ? MissionTracker.Instance.Progreso(mision.Id) : null;
        string estado = completada ? "✔ Completada" : "En curso";
        if (!completada && progreso != null) estado += $" {progreso}";

        // La activa se marca aquí también: el cartel de HDU-16 nombra una sola misión
        // y esta página las lista todas igual, así que sin la marca no hay forma de
        // saber cuál de estas tres es la que el cartel está contando.
        bool esActiva = MissionManager.Instance != null &&
                        MissionManager.Instance.Activa == mision;
        // Sin símbolo: Mango es una fuente de rótulo y no trae ojivas ni rombos, y un
        // carácter que le falta sale como un cuadrito hueco. Con letras siempre se lee.
        if (esActiva) estado = "ACTIVA - " + estado;

        ConstruirTexto($"{mision.Titulo} — {estado}", tituloFontSize,
            completada ? colorCompletado : colorDisponible, filaGO.transform,
            MenuTabsTheme.Fuente.Titulo);

        // El detalle de qué hay que hacer sólo aporta mientras esté pendiente.
        if (!completada)
        {
            // La misión completa manda: si trae su propia descripción, se muestra ESA
            // en vez de una línea por objetivo. Mismo criterio que MissionUIController.
            if (mision.data != null && !string.IsNullOrWhiteSpace(mision.data.descripcion))
            {
                ConstruirTexto($"   {mision.data.descripcion.Trim()}", objetivoFontSize,
                    MenuTabsTheme.Colores.ObjetivoPendiente, filaGO.transform,
                    MenuTabsTheme.Fuente.Objetivos);
            }
            else
            {
                foreach (ObjetivoMision objetivo in ObjetivosDe(mision.Id))
                {
                    string marca = objetivo.cumplido ? "✔" : "•";
                    Color color = objetivo.cumplido
                        ? colorCompletado
                        : MenuTabsTheme.Colores.ObjetivoPendiente;
                    ConstruirTexto($"   {marca} {objetivo.Describir()}", objetivoFontSize,
                        color, filaGO.transform, MenuTabsTheme.Fuente.Objetivos);
                }
            }
        }

        return filaGO;
    }

    private static IReadOnlyList<ObjetivoMision> ObjetivosDe(string desafioId)
    {
        return MissionTracker.Instance != null
            ? MissionTracker.Instance.Objetivos(desafioId)
            : new List<ObjetivoMision>();
    }

    private GameObject MissionTemplateRow(DesafioRuntime mision)
    {
        bool done = mision.estado == EstadoDesafio.Completado;
        string progress = MissionTracker.Instance != null ? MissionTracker.Instance.Progreso(mision.Id) : null;
        string status = done ? "Completada" : "En curso";
        if (!done && progress != null) status += " " + progress;
        if (MissionManager.Instance != null && MissionManager.Instance.Activa == mision)
            status = "ACTIVA - " + status;
        var description = new System.Text.StringBuilder(status);
        if (!done)
        {
            // La misión completa manda: si trae su propia descripción, se muestra ESA
            // en vez de la lista con contador y color.
            if (mision.data != null && !string.IsNullOrWhiteSpace(mision.data.descripcion))
            {
                description.Append("\n").Append(mision.data.descripcion.Trim());
            }
            else
            {
                // Sin "Pendiente"/"Completado" delante: el avance lo dice el contador, y el
                // objetivo ya cumplido se pinta en verde.
                string verde = ColorUtility.ToHtmlStringRGB(colorCompletado);
                foreach (var objective in ObjetivosDe(mision.Id))
                {
                    string line = ConProgreso(objective);
                    description.Append("\n").Append(objective.cumplido ? $"<color=#{verde}>{line}</color>" : line);
                }
            }
        }
        return TemplateRow(mision.Titulo, description.ToString(), done ? colorCompletado : (Color?)null);
    }

    /// <summary>
    /// El objetivo con su avance siempre a la vista. "Juntar" ya trae su contador
    /// —"Juntar Concha (1/3)"—; el resto se cumple de una vez, así que cuenta 0/1 o 1/1.
    /// </summary>
    private static string ConProgreso(ObjetivoMision objetivo)
    {
        string texto = objetivo.Describir();
        if (objetivo.tipo == TipoObjetivo.RecogerObjeto) return texto;
        return $"{texto} ({(objetivo.cumplido ? 1 : 0)}/1)";
    }

    /// <summary>Clona la fila de ejemplo. Con <paramref name="color"/> se pinta la
    /// fila entera; sin él se queda con los colores del diseño.</summary>
    private GameObject TemplateRow(string title, string description, Color? color = null)
    {
        var row = Instantiate(rowTemplate, questContainer);
        row.name = "Mision_" + title;
        var titleTransform = row.transform.Find("Titulo");
        var titleLabel = titleTransform != null ? titleTransform.GetComponent<TMP_Text>() : null;
        foreach (var label in row.GetComponentsInChildren<TMP_Text>(true))
        {
            label.text = label == titleLabel ? title : description;
            label.raycastTarget = false;
            if (color.HasValue) label.color = color.Value;
        }
        row.SetActive(true);
        return row;
    }

    /// <summary>
    /// Una línea de la lista. La fuente entra como parámetro porque los títulos van
    /// en Mango —la de la marca, la misma de las pestañas— y los objetivos en la de
    /// cuerpo: Mango es de rótulo y cansa en una lista de frases.
    ///
    /// Si no se pasa ninguna, TMP usa su fuente por defecto, que es la que hacía que
    /// esta página pareciera de otro programa.
    /// </summary>
    private GameObject ConstruirTexto(string contenido, float tamano, Color color,
        Transform padre, TMP_FontAsset fuente = null)
    {
        var go = new GameObject("Linea", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(padre, false);

        var texto = go.AddComponent<TextMeshProUGUI>();
        texto.text = contenido;
        texto.fontSize = tamano;
        texto.color = color;
        texto.alignment = TextAlignmentOptions.TopLeft;
        if (fuente != null) texto.font = fuente;

        // Sin una altura mínima el layout colapsa la fila antes de que TMP mida.
        go.GetComponent<LayoutElement>().minHeight = tamano * 1.4f;
        return go;
    }

    private Transform CrearContenedor()
    {
        var go = new GameObject("QuestContainer", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(16f, 16f);
        rt.offsetMax = new Vector2(-16f, -16f);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;
        return rt;
    }
}
