using UnityEngine;

/// <summary>
/// Datos de un item del inventario.
/// Se crea como asset en el Project: boton derecho > Create > Inventory > Item.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Datos del item")]
    [Tooltip("Nombre del item.")]
    public string itemName = "Item";

    [Tooltip("Icono que se mostrara en la UI.")]
    public Sprite icon;

    [Tooltip("Descripcion opcional.")]
    [TextArea(2, 5)]
    public string description;
}
