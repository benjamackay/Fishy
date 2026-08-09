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
    public string itemIcon;
    public string itemDescription;
    public ItemType itemType; //definir el objeto

}
