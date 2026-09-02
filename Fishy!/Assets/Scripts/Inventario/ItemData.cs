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
    public string itemName;
    // Era un string: como Sprite el inventario puede dibujarlo sin resolver rutas.
    [Tooltip("Icono que se muestra en la casilla del inventario.")]
    public Sprite itemIcon;
    [TextArea]
    public string itemDescription;
    public ItemType itemType; //definir el objeto

}
