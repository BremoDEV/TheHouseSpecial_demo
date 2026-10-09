using UnityEngine;

public enum ItemCategory
{
    Drink,
    Food
}

// Un producto de la carta (bebida o plato).
// Se crea desde el Project: clic derecho -> Create -> Restaurante -> Producto
[CreateAssetMenu(fileName = "NuevoProducto", menuName = "Restaurante/Producto")]
public class MenuItem : ScriptableObject
{
    [Tooltip("Nombre que sale en el botón del TPV")]
    public string displayName = "Producto";

    public ItemCategory category = ItemCategory.Food;

    [Tooltip("Icono para bocadillos, manos y barra. Si no hay, se usa el color.")]
    public Sprite icon;

    [Tooltip("Color provisional mientras no hay iconos")]
    public Color placeholderColor = Color.white;

    [Header("Solo para platos especiales")]
    [Tooltip("Si este es un plato especial, el plato normal al que sustituye (p. ej. Pollo asado). " +
             "Así los clientes que pidieron el normal aceptan el especial.")]
    public MenuItem replaces;

    [Tooltip("Multiplicador de precio. 1 = normal, 1.5 = un 50% más caro")]
    [Min(0f)] public float priceMultiplier = 1f;

    // ¿Sirve este producto para un cliente que pidió 'other'?
    public bool Matches(MenuItem other)
    {
        if (other == null) return false;
        return other == this || replaces == other || other.replaces == this;
    }
}
