using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mochila de Otto. Singleton persistente entre escenas, igual que ApiManager
/// y MissionManager.
///
/// Quien recoge cosas del suelo es <see cref="WorldItem"/>; quien las dibuja es
/// <see cref="InventoryManagerUI"/>, que se entera de los cambios por
/// <see cref="OnInventoryChanged"/> (si la UI tuviera que preguntar en cada
/// frame, o peor, refrescarse sólo al abrir el Tab, el jugador vería la casilla
/// aparecer tarde).
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;
    public List<Item> inventory = new List<Item>();

    /// <summary>Se dispara cada vez que el contenido cambia.</summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// Acceso seguro al singleton: si nadie puso un InventoryManager en la
    /// escena, lo crea. Así recoger un objeto nunca revienta por un montaje
    /// incompleto (mismo truco que iniciar.cs con el ApiManager).
    /// </summary>
    public static InventoryManager Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindAnyObjectByType<InventoryManager>();
            if (instance != null) return instance;

            // Awake se encarga de fijar 'instance' y del DontDestroyOnLoad.
            return new GameObject("InventoryManager").AddComponent<InventoryManager>();
        }
    }

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
        if (itemData == null || quantity <= 0) return;

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
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Cuántas unidades hay de un objeto (0 si no está).</summary>
    public int GetQuantity(ItemData itemData)
    {
        if (itemData == null) return 0;
        Item item = inventory.Find(i => i.itemData == itemData);
        return item != null ? item.itemQuantity : 0;
    }
}
