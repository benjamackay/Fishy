using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HDU-12 CA2 / HDU-11 — Monta los dos álbumes como pantallas del celular de Otto:
/// uno para las <b>evidencias</b> y otro para las <b>recompensas</b> (los pines).
///
/// <b>Por qué dos y no uno.</b> Se ganan de forma distinta —el pin pide acertar más
/// de la mitad de las señales de un caso; una evidencia la deja UNA señal bien
/// marcada— y en una sola pantalla cada estado vacío tenía que disculparse por la
/// mitad del contenido. Separados, cada uno dice una cosa sola y la dice entera.
///
/// <b>Pero solo uno tiene botón en el menú, y es a propósito.</b> Evidencias sí:
/// es lo que el niño/a va llenando caso a caso y a lo que vuelve a consultar.
/// Recompensas no: se entra desde la mochila, pulsando el ítem «Álbum» —ver
/// <see cref="AlbumEvidenciasUI.Show"/>—, que es por donde se entraba desde HDU-11 y
/// donde ese ítem ya estaba. Así el inicio del celular se queda en siete botones en
/// vez de ocho, y no hay dos puertas compitiendo para la misma pantalla.
/// Las dos páginas se crean igual: la de recompensas existe y tiene su índice, solo
/// que quien la abre es la mochila.
///
/// <b>Por qué el botón se clona y la página se calca.</b> Los botones del celular son
/// arte de la escena dentro de un <see cref="GridLayoutGroup"/> de cuatro columnas, y
/// las páginas tienen su fondo y su marco. Clonando el botón sale idéntico y la
/// cuadrícula lo coloca sola; copiando el aspecto de una página que ya exista, las
/// nuevas siguen el arte del teléfono aunque alguien lo cambie mañana.
///
/// <b>Por qué por código y no tocando el prefab.</b> Editar el .prefab a mano es YAML
/// con fileIDs, y <see cref="PhoneMenuNavigation"/> empareja botones con páginas POR
/// ÍNDICE: un error ahí abre la página equivocada y no se nota hasta que alguien lo
/// prueba.
/// </summary>
public static class AccesoAlbumEnElCelular
{
    // ── Nombres de lo que se crea ─────────────────────────────────────────────

    /// <summary>El único botón que se añade al inicio del celular. Recompensas no
    /// tiene: se entra por la mochila (ver la nota de la clase).</summary>
    public const string NombreBotonEvidencias = "BotonAlbumEvidencias";

    public const string NombrePaginaEvidencias = "AlbumEvidenciasPage";
    public const string NombrePaginaRecompensas = "AlbumRecompensasPage";

    /// <summary>Lo que se lee en el encabezado del celular al abrir cada página.</summary>
    public const string TituloEvidencias = "Evidencias";
    public const string TituloRecompensas = "Recompensas";

    /// <summary>
    /// Dónde se busca el ícono de trazo que va dentro del botón.
    ///
    /// Tiene que estar en <c>Assets/Resources/</c> y no en <c>Sprites/Iconos/</c>
    /// como sus seis vecinos: a esos los referencia el prefab, que se serializa; a
    /// este lo pide código que corre en runtime, y lo único que se puede cargar por
    /// nombre desde código es Resources. Si falta, el botón se queda con el ícono del
    /// que se clonó y se avisa en consola — se ve raro, pero funciona.
    /// </summary>
    public const string RutaIconoEvidencias = "Iconos/evidencias";

    /// <summary>De cuál se copia el aspecto del botón. Si no está, se copia del
    /// último que haya: lo que importa es que el nuevo se parezca a sus vecinos.</summary>
    private const string Plantilla = "BotonInventario";

    /// <summary>En qué posición del celular quedó cada página. -1 = todavía no se
    /// colocó (no hay celular en esta escena). Lo lee <see cref="AlbumEvidenciasUI"/>
    /// para que el ítem de la mochila siga abriendo el álbum de evidencias.</summary>
    public static int IndiceEvidencias { get; private set; } = -1;
    public static int IndiceRecompensas { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarEstadoEstatico()
    {
        // Los índices son de la escena anterior; el editor conserva los estáticos
        // entre corridas de Play y abrir la página 7 de un celular que ya no existe
        // es un IndexOutOfRange con mala pinta.
        IndiceEvidencias = -1;
        IndiceRecompensas = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arrancar()
    {
        // El `-=` antes del `+=` no es paranoia: el evento es estático y sobrevive al
        // recargado de dominio del editor. Mismo motivo que en SaveManager.AutoCrear.
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;

        Colocar();
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => Colocar();

    /// <summary>
    /// Pone los dos álbumes en el celular de esta escena, si hay uno y todavía no los
    /// tiene. En las escenas sin celular —el login, los menús— no hace nada y no se
    /// queja.
    /// </summary>
    public static void Colocar()
    {
        // Incluyendo los inactivos: el celular arranca apagado y se enciende al
        // pulsar su botón, así que buscándolo activo no se encontraría nunca.
        Colocar(UnityEngine.Object.FindAnyObjectByType<PhoneMenuNavigation>(
            FindObjectsInactive.Include));
    }

    /// <summary>
    /// Igual, sobre un celular concreto. Devuelve true si los álbumes quedaron
    /// puestos en esta llamada; false si no había celular, si ya los tenía o si no se
    /// pudo.
    ///
    /// La sobrecarga existe para las pruebas: buscar «el celular de la escena» en un
    /// arnés que acaba de instanciar el suyo puede devolver el de la escena que el
    /// desarrollador tenía abierta.
    /// </summary>
    public static bool Colocar(PhoneMenuNavigation celular)
    {
        if (celular == null || celular.home == null) return false;
        if (celular.pages == null || celular.pages.Length == 0) return false;

        Transform inicio = celular.home.transform;
        if (inicio.Find(NombreBotonEvidencias) != null) return false;

        Button plantilla = BuscarPlantilla(inicio);
        if (plantilla == null)
        {
            Debug.LogWarning("[Álbum] El celular no tiene ningún botón del que copiar el " +
                             "aspecto: los álbumes se quedan fuera del menú.", celular);
            return false;
        }

        // El aspecto de las páginas se copia de una que ya exista. Se hace ANTES de
        // crear ninguna: las páginas nuevas lo leen al construirse.
        PaginaDeAlbum.TomarAspectoDe(celular.pages[0]);

        IndiceEvidencias = AnadirPagina<PaginaDeEvidencias>(
            celular, NombrePaginaEvidencias, TituloEvidencias);
        IndiceRecompensas = AnadirPagina<PaginaDeRecompensas>(
            celular, NombrePaginaRecompensas, TituloRecompensas);

        // Solo evidencias gana botón. A recompensas se entra desde la mochila; su
        // página ya está creada y con su índice, esperando a que la abran.
        AnadirBoton(inicio, plantilla, NombreBotonEvidencias, TituloEvidencias,
            RutaIconoEvidencias, celular, IndiceEvidencias);

        return true;
    }

    // ── Las páginas ───────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una página dentro del contenedor de páginas del celular y la añade a sus
    /// listas. Devuelve el índice con el que se abre.
    ///
    /// Las dos listas crecen a la vez y en el mismo orden a propósito:
    /// <c>PhoneMenuNavigation.ShowPage</c> las empareja por índice, y dejarlas
    /// descuadradas pone el título de una página sobre el contenido de otra.
    /// </summary>
    private static int AnadirPagina<T>(PhoneMenuNavigation celular, string nombre, string titulo)
        where T : PaginaDeAlbum
    {
        Transform contenedor = celular.pages[0].transform.parent;

        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(contenedor, false);

        // Como las demás páginas del prefab: ocupa todo el hueco de 'Pages'.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Apagada antes de añadir el componente: así su OnEnable —que es donde se
        // repuebla— no corre hasta que alguien entre de verdad.
        go.SetActive(false);
        go.AddComponent<T>();

        int indice = celular.pages.Length;
        Array.Resize(ref celular.pages, indice + 1);
        celular.pages[indice] = go;

        if (celular.pageTitles == null) celular.pageTitles = new string[0];
        Array.Resize(ref celular.pageTitles, indice + 1);
        celular.pageTitles[indice] = titulo;

        return indice;
    }

    // ── Los botones ───────────────────────────────────────────────────────────

    private static Button BuscarPlantilla(Transform inicio)
    {
        Transform preferida = inicio.Find(Plantilla);
        if (preferida != null)
        {
            var botonPreferido = preferida.GetComponent<Button>();
            if (botonPreferido != null) return botonPreferido;
        }

        // Al revés: el último es el que mejor representa cómo quedó la fila.
        for (int i = inicio.childCount - 1; i >= 0; i--)
        {
            var boton = inicio.GetChild(i).GetComponent<Button>();
            if (boton != null) return boton;
        }

        return null;
    }

    private static void AnadirBoton(Transform inicio, Button plantilla, string nombre,
        string etiqueta, string rutaIcono, PhoneMenuNavigation celular, int indicePagina)
    {
        Button boton = UnityEngine.Object.Instantiate(plantilla, inicio);
        boton.gameObject.name = nombre;
        boton.transform.SetAsLastSibling();   // la cuadrícula lo coloca sola

        Renombrar(boton, etiqueta);
        PonerIcono(boton, rutaIcono, nombre);
        Reconectar(boton, celular, indicePagina);
    }

    /// <summary>Pone la etiqueta del botón. En el prefab actual esos textos vienen
    /// DESACTIVADOS —los botones se distinguen solo por el dibujo—, así que esto hoy
    /// no se ve; se escribe igual para que el día que se enciendan no haya que volver
    /// aquí, y porque es lo que leería un lector de pantalla.</summary>
    private static void Renombrar(Button boton, string texto)
    {
        var etiqueta = boton.GetComponentInChildren<TMP_Text>(true);
        if (etiqueta != null) etiqueta.text = texto;
    }

    /// <summary>
    /// Le pone su ícono de trazo.
    ///
    /// <b>Va en el hijo "Icono", no en "Fondo".</b> Cada botón del celular es
    /// Botón(borde) → Fondo(relleno) → Icono(el dibujo). Antes esto cogía el primer
    /// Image hijo, que es <c>Fondo</c>, y le encajaba encima el sprite a color del
    /// ítem de la mochila: el resultado era una mochila coloreada donde los vecinos
    /// tienen un trazo crema, y el botón cantaba desde el otro lado de la sala.
    /// </summary>
    private static void PonerIcono(Button boton, string rutaIcono, string nombreBoton)
    {
        var icono = Resources.Load<Sprite>(rutaIcono);
        if (icono == null)
        {
            Debug.LogWarning(
                $"[Álbum] Falta el ícono de '{nombreBoton}': se esperaba un sprite en " +
                $"Assets/Resources/{rutaIcono}.png. El botón se queda con el dibujo del " +
                "que se clonó, así que se parecerá a otro del menú.");
            return;
        }

        Transform destino = BuscarIcono(boton.transform);
        if (destino == null) return;

        var img = destino.GetComponent<Image>();
        img.sprite = icono;
        img.preserveAspect = true;
    }

    /// <summary>El hijo que lleva el dibujo. Se busca por nombre —así se llama en el
    /// prefab— y, si no estuviera, se cae al Image más profundo, que es donde vive el
    /// ícono en esta clase de botones.</summary>
    private static Transform BuscarIcono(Transform boton)
    {
        Transform porNombre = BuscarRecursivo(boton, "Icono");
        if (porNombre != null && porNombre.GetComponent<Image>() != null) return porNombre;

        Transform masProfundo = null;
        int profundidadMax = -1;
        foreach (Image img in boton.GetComponentsInChildren<Image>(true))
        {
            if (img.transform == boton) continue;

            int profundidad = 0;
            for (Transform t = img.transform; t != boton && t != null; t = t.parent) profundidad++;
            if (profundidad <= profundidadMax) continue;

            profundidadMax = profundidad;
            masProfundo = img.transform;
        }
        return masProfundo;
    }

    private static Transform BuscarRecursivo(Transform padre, string nombre)
    {
        foreach (Transform t in padre.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;

        return null;
    }

    /// <summary>
    /// Deja el botón abriendo SU página y nada más.
    ///
    /// <c>RemoveAllListeners</c> por sí solo no alcanza: el botón clonado trae el
    /// <c>onClick</c> que el prefab tiene guardado —una llamada a
    /// <c>PhoneMenuNavigation.ShowPage</c> con el índice del que se copió—, y esos son
    /// <i>persistentes</i>: se serializan con el objeto y ese método no los toca. Sin
    /// apagarlos uno a uno, pulsar «Evidencias» abriría además el inventario.
    /// </summary>
    private static void Reconectar(Button boton, PhoneMenuNavigation celular, int indicePagina)
    {
        for (int i = 0; i < boton.onClick.GetPersistentEventCount(); i++)
            boton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);

        boton.onClick.RemoveAllListeners();
        boton.onClick.AddListener(() => celular.ShowPage(indicePagina));
    }

    // ── Para quien quiera abrirlos desde fuera ────────────────────────────────

    /// <summary>Abre el álbum de evidencias en el celular. Lo usa el ítem de la
    /// mochila. Devuelve false si en esta escena no hay celular con álbumes.</summary>
    public static bool AbrirEvidencias() => Abrir(IndiceEvidencias);

    public static bool AbrirRecompensas() => Abrir(IndiceRecompensas);

    private static bool Abrir(int indice)
    {
        if (indice < 0) return false;

        var celular = UnityEngine.Object.FindAnyObjectByType<PhoneMenuNavigation>(
            FindObjectsInactive.Include);
        if (celular == null || celular.pages == null || indice >= celular.pages.Length)
            return false;

        celular.ShowPage(indice);
        return true;
    }
}
