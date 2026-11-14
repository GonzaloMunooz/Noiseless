using UnityEngine;

/// <summary>
/// Item que el jugador puede recoger. Se pone en el mismo objeto que el InteractableObject
/// y su metodo PickUp() se engancha al evento 'onInteract' desde el Inspector.
/// Al recogerlo se suma al inventario del jugador y el objeto desaparece.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Tooltip("Item que se añade al inventario al recogerlo.")]
    [SerializeField] private ItemData item;

    private PlayerInventoryController inventory;

    private void Awake()
    {
        inventory = FindAnyObjectByType<PlayerInventoryController>();
        if (inventory == null)
        {
            Debug.LogWarning("ItemPickup: no hay ningun PlayerInventoryController en la escena.", this);
        }

        if (item == null)
        {
            Debug.LogWarning("ItemPickup: falta asignar el ItemData de '" + name + "'.", this);
        }
    }

    // Se llama desde el evento onInteract del InteractableObject
    public void PickUp()
    {
        if (item == null || inventory == null)
        {
            return;
        }

        inventory.AddItem(item);

        // Destruye el objeto del mundo (y con el su etiqueta "[E]")
        Destroy(gameObject);
    }
}
