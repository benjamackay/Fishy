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

    /// <summary>Partida dueña de lo que hay en la mochila. Ver <see cref="ConfigurarParaPartida"/>.</summary>
    private static int partidaActual;

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

    /// <summary>
    /// Ata la mochila a una partida concreta y la vacía si se cambió de partida.
    ///
    /// Sin esto, pasar del Perfil 1 al Perfil 2 dentro de la misma ejecución le
    /// dejaba al segundo niño/a lo que había recogido el primero: el singleton es
    /// DontDestroyOnLoad y nadie lo limpiaba. <see cref="Fishy.Mision.MissionManager"/>
    /// ya se protegía así en ConfigurarPersistenciaParaPartida; esto es lo mismo
    /// para el inventario.
    ///
    /// Es estático a propósito: se llama al elegir el perfil, que ocurre en la
    /// escena de ingreso, donde el InventoryManager todavía puede no existir. Si
    /// aún no hay instancia no hay nada que vaciar —la que se cree después nacerá
    /// vacía— pero la partida queda anotada igual.
    /// </summary>
    public static void ConfigurarParaPartida(int partidaId)
    {
        if (partidaId <= 0)
        {
            Debug.LogWarning("[Inventario] No se puede configurar una PartidaId inválida.");
            return;
        }

        if (partidaId == partidaActual) return;

        partidaActual = partidaId;
        if (instance != null) instance.Vaciar();

        Debug.Log($"[Inventario] Mochila asociada a la partida {partidaId}.");
    }

    /// <summary>Deja la mochila vacía y avisa a la UI.</summary>
    public void Vaciar()
    {
        if (inventory.Count == 0) return;
        inventory.Clear();
        OnInventoryChanged?.Invoke();
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
