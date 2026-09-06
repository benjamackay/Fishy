using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encuentra un <see cref="ItemData"/> por su <c>itemId</c>.
///
/// Hace falta para restaurar la mochila: del backend vuelve un id de texto
/// (<c>ITEM_BRUJULA</c>) y para ponerlo en el inventario se necesita el asset,
/// que es lo que trae el nombre visible y el ícono.
///
/// <b>Por qué los items viven en <c>Resources/Items/</c>:</b> es la única forma de
/// cargar un asset por código sin que alguien lo haya arrastrado antes a la escena,
/// y aquí no hay quien lo arrastre —el objeto viene de la base de datos, no del
/// mapa—. Mismo mecanismo que ya usan el banco de preguntas, los casos del Modo
/// Detective y los fondos. Se carga la carpeta entera con <c>LoadAll</c>, así que
/// un objeto nuevo entra solo: basta con crear el asset ahí y ponerle su itemId.
///
/// El diccionario se arma una vez y se queda. Son nueve assets; recargarlo en cada
/// consulta sería leer disco para nada.
/// </summary>
public static class CatalogoItems
{
    /// <summary>Carpeta dentro de Resources donde viven los ItemData.</summary>
    public const string CarpetaResources = "Items";

    private static Dictionary<string, ItemData> _porId;

    /// <summary>ItemData con ese itemId, o null si no existe ninguno.</summary>
    public static ItemData Buscar(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        Asegurar();
        return _porId.TryGetValue(itemId.Trim(), out var data) ? data : null;
    }

    /// <summary>Todos los ItemData con itemId válido, por id.</summary>
    public static IReadOnlyDictionary<string, ItemData> Todos
    {
        get { Asegurar(); return _porId; }
    }

    /// <summary>
    /// Vuelve a leer la carpeta. Solo hace falta si se crean assets en caliente
    /// desde el editor; en el juego el catálogo no cambia durante la partida.
    /// </summary>
    public static void Recargar()
    {
        _porId = null;
        Asegurar();
    }

    private static void Asegurar()
    {
        if (_porId != null) return;

        _porId = new Dictionary<string, ItemData>();

        foreach (var data in Resources.LoadAll<ItemData>(CarpetaResources))
        {
            if (data == null) continue;

            if (string.IsNullOrWhiteSpace(data.itemId))
            {
                // Sin id no se puede guardar ni restaurar. Se avisa una vez al armar
                // el catálogo en vez de callarlo hasta que el objeto desaparezca de
                // la mochila de un niño/a sin explicación.
                Debug.LogWarning(
                    $"[CatalogoItems] '{data.name}' no tiene itemId: no se va a poder " +
                    "guardar. Ponle uno en el asset (Fishy → Revisar ids de objetos).");
                continue;
            }

            string id = data.itemId.Trim();
            if (_porId.TryGetValue(id, out var previo))
            {
                // Dos assets con el mismo id: al restaurar saldría uno de los dos, y
                // cuál depende del orden en que Unity los devuelva. Es justo el
                // desajuste silencioso que la herramienta de editor busca evitar.
                Debug.LogError(
                    $"[CatalogoItems] '{id}' está repetido: '{previo.name}' y '{data.name}'. " +
                    "Se queda el primero. Corrígelo con Fishy → Revisar ids de objetos.");
                continue;
            }

            _porId[id] = data;
        }

        Debug.Log($"[CatalogoItems] {_porId.Count} objeto(s) en el catálogo.");
    }
}
