using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;
    public List<Item> inventory = new List<Item>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void AddItem(ItemData itemData, int quantity)
    {
        Item existingItem = inventory.Find(item => item.itemData == itemData);
        if (existingItem != null)
        {
            existingItem.itemQuantity += quantity;
        }
        else
        {
            Item newItem = new Item
            {
                itemData = itemData,
                itemQuantity = quantity
            };
            inventory.Add(newItem);
        }
    }
}
