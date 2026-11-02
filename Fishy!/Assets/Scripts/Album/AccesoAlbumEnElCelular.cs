using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HDU-12 CA2 — El álbum se abre desde el menú principal del juego: el celular de
/// Otto, junto a Perfil, Misiones, Inventario, Mapa, Diccionario y Bloc de notas.
///
/// <b>Por qué hacía falta otra puerta.</b> Hasta ahora la única era el ítem «Álbum
/// de Evidencias» dentro de la mochila, que es una pantalla dentro de otra: hay que
/// abrir el celular, entrar a Inventario, encontrar el ítem entre los demás y
/// pulsarlo. El criterio pide que el álbum esté <i>en el menú</i>, y para un niño/a
/// de 9 años un ítem escondido en una cuadrícula no es un menú. La mochila se deja
/// igual: quien ya sabía abrirlo por ahí lo sigue abriendo por ahí.
///
/// <b>Por qué el botón se clona y no se dibuja.</b> Los seis botones del celular son
/// arte de la escena —su marco, su tipografía, su tamaño— dentro de un
/// <see cref="GridLayoutGroup"/> de cuatro columnas. Un botón armado con
/// <c>FishyUIKit</c> se vería como de otro juego al lado de ellos. Clonando uno sale
/// idéntico y la cuadrícula lo coloca sola, sin coordenadas escritas a mano que haya
/// que corregir el día que alguien añada un séptimo.
///
/// <b>Por qué se añade por código y no en el prefab.</b> Tocar el .prefab a mano es
/// editar YAML con fileIDs, y el <c>PhoneMenuNavigation</c> empareja botones con
/// páginas por índice: un error ahí abre la página equivocada y no se nota hasta que
/// alguien lo prueba. El álbum además no es una página del celular —se abre en su
/// propio canvas, por encima—, así que no tiene índice que ocupar.
/// </summary>
public static class AccesoAlbumEnElCelular
{
    /// <summary>Nombre del botón que se crea. Se mira antes de crear otro: el
    /// bootstrap corre en cada escena y el celular sobrevive a algunas.</summary>
    public const string NombreBoton = "BotonAlbum";

    private const string Etiqueta = "Álbum";

    /// <summary>De cuál se copia el aspecto. Si no está, se copia del último botón
    /// que haya: lo que importa es que el nuevo se parezca a sus vecinos.</summary>
    private const string Plantilla = "BotonInventario";

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
    /// Pone el botón en el celular de esta escena, si hay uno y todavía no lo tiene.
    /// En las escenas sin celular —el login, los menús— no hace nada y no se queja.
    /// </summary>
    public static void Colocar()
    {
        // Incluyendo los inactivos: el celular arranca apagado y se enciende al
        // pulsar su botón, así que buscándolo activo no se encontraría nunca.
        Colocar(Object.FindAnyObjectByType<PhoneMenuNavigation>(FindObjectsInactive.Include));
    }

    /// <summary>
    /// Igual, sobre un celular concreto. Devuelve true si el botón quedó puesto en
    /// esta llamada; false si no había celular, si ya lo tenía o si no se pudo.
    ///
    /// La sobrecarga existe para las pruebas: buscar «el celular de la escena» en un
    /// arnés que acaba de instanciar el suyo puede devolver el de la escena que el
    /// desarrollador tenía abierta.
    /// </summary>
    public static bool Colocar(PhoneMenuNavigation celular)
    {
        if (celular == null || celular.home == null) return false;

        Transform inicio = celular.home.transform;
        if (inicio.Find(NombreBoton) != null) return false;

        Button plantilla = BuscarPlantilla(inicio);
        if (plantilla == null)
        {
            Debug.LogWarning("[Álbum] El celular no tiene ningún botón del que copiar el " +
                             "aspecto: el álbum se sigue abriendo desde la mochila.", celular);
            return false;
        }

        Button boton = Object.Instantiate(plantilla, inicio);
        boton.gameObject.name = NombreBoton;
        boton.transform.SetAsLastSibling();   // la cuadrícula lo coloca sola

        Renombrar(boton, Etiqueta);
        PonerIcono(boton);
        Reconectar(boton);
        return true;
    }

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

    private static void Renombrar(Button boton, string texto)
    {
        var etiqueta = boton.GetComponentInChildren<TMP_Text>(true);
        if (etiqueta != null) etiqueta.text = texto;
    }

    /// <summary>
    /// Le pone el ícono del propio ítem «Álbum de Evidencias», el mismo que se ve en
    /// la mochila y en el detalle de un pin. Sin esto el botón nuevo tendría el
    /// dibujo de la mochila y habría dos botones iguales con nombres distintos, que
    /// es exactamente el tipo de pantalla en la que un niño/a pulsa el que no era.
    ///
    /// Si el ItemData no está, se deja el dibujo copiado: un botón con el icono
    /// equivocado y el nombre correcto todavía se puede usar; uno sin dibujo, no.
    /// </summary>
    private static void PonerIcono(Button boton)
    {
        ItemData datos = CatalogoItems.Buscar(AlbumEvidenciasUI.ItemIdAlbum);
        if (datos == null || datos.itemIcon == null) return;

        // El primer Image que no sea el del propio botón: en estos botones el dibujo
        // está en un hijo llamado "Fondo" (ver el prefab PhoneMenu).
        foreach (Image img in boton.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject == boton.gameObject) continue;

            img.sprite = datos.itemIcon;
            img.preserveAspect = true;
            return;
        }
    }

    /// <summary>
    /// Deja el botón abriendo el álbum y nada más.
    ///
    /// <c>RemoveAllListeners</c> por sí solo no alcanza: el botón clonado trae el
    /// <c>onClick</c> que el prefab tiene guardado —una llamada a
    /// <c>PhoneMenuNavigation.ShowPage</c> con el índice del inventario—, y esos son
    /// <i>persistentes</i>, se serializan con el objeto y ese método no los toca. Sin
    /// apagarlos uno a uno, pulsar «Álbum» abriría además la página del inventario.
    /// </summary>
    private static void Reconectar(Button boton)
    {
        for (int i = 0; i < boton.onClick.GetPersistentEventCount(); i++)
            boton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);

        boton.onClick.RemoveAllListeners();
        boton.onClick.AddListener(AlbumEvidenciasUI.Show);
    }
}
