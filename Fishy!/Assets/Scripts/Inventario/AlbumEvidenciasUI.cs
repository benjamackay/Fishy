using UnityEngine;

/// <summary>
/// HDU-12 — La puerta del álbum desde la mochila, y los dos ids que el resto del
/// juego necesita conocer.
///
/// <b>Esta clase ya no dibuja nada.</b> Era la pantalla entera del álbum: una ventana
/// propia de 900×700 en su canvas (sortingOrder 1300), que se abría ENCIMA del menú
/// del celular (1200). El niño/a pulsaba un botón del teléfono y lo que aparecía era
/// otra cosa, de otro tamaño y con otro marco, tapando el teléfono.
///
/// Ahora los álbumes son dos pantallas DEL celular —<see cref="PaginaDeEvidencias"/>
/// y <see cref="PaginaDeRecompensas"/>, montadas por
/// <see cref="AccesoAlbumEnElCelular"/>— y de aquella clase queda solo lo que de
/// verdad era compartido: los dos ids y la forma de abrir el álbum desde la mochila.
///
/// Se conserva el nombre porque lo usan <c>InventoryManagerUI</c> (para saber qué
/// ranura es clicable y qué ítems no se dibujan en la mochila general) y
/// <c>DetectiveCaseManager</c> (para saber qué ItemData entregar). Renombrarla hoy
/// solo serviría para tocar cuatro archivos más.
///
/// <b>Ojo con el nombre: el ítem de la mochila abre RECOMPENSAS, no evidencias.</b>
/// El id dice <c>ITEM_ALBUM_EVIDENCIAS</c> porque es un dato que viaja en partidas ya
/// guardadas y renombrarlo las rompería, pero lo que ese ítem guarda son los pines —
/// su propia descripción lo decía desde HDU-11: «Guarda los pines de los casos del
/// Modo Detective que ya resolviste»—. A las evidencias se entra por su botón del
/// menú del celular. Por eso el <c>itemName</c> del asset sí se cambió a «Álbum de
/// Recompensas»: el nombre que se lee tiene que decir la verdad aunque el id no pueda.
/// </summary>
public static class AlbumEvidenciasUI
{
    /// <summary>itemId del ítem «Álbum» en Resources/Items. Compartido con
    /// <c>InventoryManagerUI</c> (para saber qué ranura es clicable) y con
    /// <c>DetectiveCaseManager</c> (para saber qué ItemData entregar).</summary>
    public const string ItemIdAlbum = "ITEM_ALBUM_EVIDENCIAS";

    /// <summary>Prefijo que comparten los pines de recompensa. Público: lo usa
    /// <c>InventoryManagerUI</c> para que los pines no se dibujen en la mochila
    /// general —sólo viven en el álbum de recompensas— y
    /// <see cref="PaginaDeRecompensas"/> para saber cuáles son suyos.</summary>
    public const string PrefijoPin = "PIN_";

    /// <summary>
    /// Abre el álbum de RECOMPENSAS. Es lo que hace el ítem de la mochila, y la única
    /// puerta que tiene esa pantalla: no lleva botón en el inicio del celular.
    ///
    /// Lleva al celular a esa página en vez de abrir una ventana aparte: el ítem se
    /// pulsa DESDE la mochila, que ya es una página del teléfono, así que lo natural
    /// es cambiar de página y poder volver con el botón de volver de siempre.
    /// </summary>
    public static void Show()
    {
        if (AccesoAlbumEnElCelular.AbrirRecompensas()) return;

        // Sin celular en la escena no hay dónde enseñarlo. No es un error del
        // jugador ni rompe nada: pasa en escenas de prueba y en los arneses.
        Debug.LogWarning("[Álbum] No hay un celular en esta escena al que llevar el " +
                         "álbum de recompensas.");
    }
}
