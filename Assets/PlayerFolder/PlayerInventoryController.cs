using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventario del jugador. Por ahora solo guarda la lista de items recogidos,
/// sin UI: cada vez que se recoge algo se avisa por consola.
/// Se pone en el Player.
/// </summary>
public class PlayerInventoryController : MonoBehaviour
{
    private List<ItemData> items = new List<ItemData>();

    public IReadOnlyList<ItemData> Items
    {
        get { return items; }
    }

    public int ItemCount
    {
        get { return items.Count; }
    }

    public void AddItem(ItemData item)
    {
        if (item == null)
        {
            return;
        }

        items.Add(item);

        Debug.Log("Inventario: recogido '" + item.itemName + "'. Items en el inventario: " + items.Count, this);
    }

    public bool RemoveItem(ItemData item)
    {
        return items.Remove(item);
    }

    public bool HasItem(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        return items.Contains(item);
    }
}
