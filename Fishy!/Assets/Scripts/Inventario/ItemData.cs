using UnityEngine;
[CreateAssetMenu(fileName ="ItemData", menuName = "Inventory/New Item")]


//Generar una plantilla de objetos
public class ItemData : ScriptableObject //Heredar
{
    public enum ItemType
    {
        Consumable,
        Equipment,
    }
    /// <summary>
    /// Identificador único del objeto, y lo único de esta ficha que viaja al backend.
    ///
    /// Hace falta porque el inventario guarda una <b>referencia</b> a este asset, y una
    /// referencia no se puede escribir en una fila de Postgres: para decir "esta partida
    /// tiene 2 de esto" hay que poder nombrar el "esto". <see cref="itemName"/> no sirve
    /// —es texto de pantalla, hecho para cambiar—, así que renombrar un objeto dejaría
    /// los inventarios guardados apuntando a nada, y en silencio.
    ///
    /// Misma convención que <c>DesafioData.desafioId</c>, <c>CasoDetective.caso_id</c> y
    /// el resto del proyecto: MAYÚSCULAS, sin tildes ni espacios, y <b>no se renombra</b>
    /// una vez que hay partidas guardadas con él.
    /// </summary>
    [Tooltip("Identificador único del objeto. Ej: 'ITEM_BRUJULA'. No cambiarlo una vez " +
             "que haya inventarios guardados: es lo que se escribe en la base de datos.")]
    public string itemId;

    [Tooltip("Nombre visible en la mochila. Este sí se puede cambiar cuando se quiera.")]
    public string itemName;
    // Era un string: como Sprite el inventario puede dibujarlo sin resolver rutas.
    [Tooltip("Icono que se muestra en la casilla del inventario.")]
    public Sprite itemIcon;
    [TextArea]
    public string itemDescription;
    public ItemType itemType; //definir el objeto

}
