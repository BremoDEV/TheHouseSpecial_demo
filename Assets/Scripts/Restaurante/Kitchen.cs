using System;
using System.Collections.Generic;
using UnityEngine;

// Plato especial: al "encargarte" de un cliente de cierto tipo, un plato se cambia por su variante.
// Ejemplo: tipo "Pájaro azul" -> Pollo asado se convierte en Pollo con plumas azules.
[Serializable]
public class SpecialVariant
{
    [Tooltip("Tipo de cliente, escrito IGUAL que el 'Type Name' de los Looks del CustomerSpawner")]
    public string customerType = "Pájaro azul";
    [Tooltip("Plato normal que desaparece de la carta")]
    public MenuItem original;
    [Tooltip("Plato especial que ocupa su lugar")]
    public MenuItem variant;
}

// La cocina: recibe los pedidos del TPV, los prepara de uno en uno
// y los deja en la parte larga de la barra en L.
// Huecos de la barra: 2 de base (4 o 6 con la mejora "Mostrador ampliado"), aunque la escena tenga más.
// Si la barra se llena, la cocina se para hasta que recojas algo, SIN perder los pedidos.
// Límite de pedidos a la vez (en barra + en cocina) = huecos de la barra + 2: de base 4, luego 6 y luego 8.
public class Kitchen : MonoBehaviour
{
    public static Kitchen Instance { get; private set; }

    [Header("Carta (los 8 productos)")]
    [SerializeField] private MenuItem[] menu;

    [Header("Platos especiales")]
    [SerializeField] private SpecialVariant[] specialVariants;

    [Header("Tiempos de preparación en segundos (editable por diseño)")]
    [SerializeField] private float drinkPrepTime = 3f;
    [SerializeField] private float foodPrepTime = 8f;

    [Header("Pedidos (editable por diseño)")]
    [Tooltip("Cuántos pedidos más que huecos de barra se pueden tener a la vez (barra + cocina). " +
             "2 = con 2 huecos puedes tener 4 pedidos; con 4 huecos, 6; con 6 huecos, 8")]
    [SerializeField] private int extraOrdersOverCounter = 2;

    [Header("Tiempo hasta que se pone malo en la barra (editable por diseño)")]
    [SerializeField] private float drinkSpoilTime = 25f;
    [SerializeField] private float foodSpoilTime = 30f;

    [Header("Barra (parte larga de la L)")]
    [Tooltip("Huecos donde aparecen los productos. El número de huecos es el límite.")]
    [SerializeField] private Transform[] counterSlots;
    [SerializeField] private CounterItem counterItemPrefab;
    [Tooltip("Dónde se pone la camarera respecto a cada hueco para recoger")]
    [SerializeField] private Vector2 waitressStandOffset = new Vector2(0f, -0.8f);

    // Avisa al TPV de que la carta ha cambiado (para rehacer los botones)
    public event Action OnMenuChanged;

    public IReadOnlyList<MenuItem> Menu => menu;
    public List<MenuItem> Drinks { get; } = new List<MenuItem>();
    public List<MenuItem> Foods { get; } = new List<MenuItem>();

    // Huecos que se pueden usar (según la mejora del mostrador, sin pasar de los que hay en la escena)
    public int Capacity => Mathf.Clamp(Upgrades.CounterCapacity, 1, counterSlots.Length);
    public int UsedCapacity => pending.Count + (cooking != null ? 1 : 0) + CountOnCounter();
    public int OnCounterCount => CountOnCounter();
    public int InKitchenCount => pending.Count + (cooking != null ? 1 : 0);
    public int MaxOrders => Capacity + extraOrdersOverCounter;

    private readonly Queue<MenuItem> pending = new Queue<MenuItem>();
    private MenuItem cooking;
    private float cookTimer;
    private CounterItem[] onCounter;

    private void Awake()
    {
        Instance = this;
        onCounter = new CounterItem[counterSlots.Length];
        RebuildCategoryLists();
    }

    private void RebuildCategoryLists()
    {
        Drinks.Clear();
        Foods.Clear();
        foreach (MenuItem item in menu)
        {
            if (item == null) continue;
            if (item.category == ItemCategory.Drink) Drinks.Add(item);
            else Foods.Add(item);
        }
    }

    public bool TryOrder(MenuItem item)
    {
        if (UsedCapacity >= MaxOrders)
        {
            Debug.Log("¡La cocina va a tope! No se pueden pedir más cosas ahora.");
            GameMessages.Show("¡La cocina va a tope! Espera a que salgan platos");
            return false;
        }
        pending.Enqueue(item);
        return true;
    }

    // Lo llama el StoreroomManager después de la pantalla en negro
    public void OnCustomerKilled(string[] customerTypes)
    {
        if (specialVariants == null || customerTypes == null) return;

        bool changed = false;
        foreach (SpecialVariant sv in specialVariants)
        {
            if (sv == null || sv.original == null || sv.variant == null) continue;
            if (!ContainsType(customerTypes, sv.customerType)) continue;

            int index = Array.IndexOf(menu, sv.original);
            if (index < 0) continue; // ya estaba cambiado

            menu[index] = sv.variant;
            changed = true;
            Debug.Log($"Nuevo en la carta: {sv.variant.displayName} (sustituye a {sv.original.displayName}).");
            GameMessages.Show($"Nuevo en la carta: {sv.variant.displayName}", 4f);
        }

        if (changed)
        {
            RebuildCategoryLists();
            OnMenuChanged?.Invoke();
        }
    }

    private static bool ContainsType(string[] types, string wanted)
    {
        if (string.IsNullOrWhiteSpace(wanted)) return false;
        foreach (string t in types)
            if (t != null && string.Equals(t.Trim(), wanted.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void Update()
    {
        if (cooking == null)
        {
            if (pending.Count > 0)
            {
                cooking = pending.Dequeue();
                cookTimer = (cooking.category == ItemCategory.Drink ? drinkPrepTime : foodPrepTime) * Upgrades.CookTimeMult;
            }
            return;
        }

        cookTimer -= GameTime.Delta;
        if (cookTimer > 0f) return;

        int slot = FindFreeSlot();
        if (slot < 0) return; // barra llena: el plato espera listo en cocina hasta que haya hueco

        PlaceOnCounter(cooking, slot);
        cooking = null;
    }

    private void PlaceOnCounter(MenuItem item, int slot)
    {
        Vector2 slotPos = counterSlots[slot].position;
        CounterItem ci = Instantiate(counterItemPrefab, slotPos, Quaternion.identity);
        float spoil = item.category == ItemCategory.Drink ? drinkSpoilTime : foodSpoilTime;
        ci.Setup(item, this, slot, slotPos + waitressStandOffset, spoil);
        onCounter[slot] = ci;
    }

    public void ClearSlot(int slot)
    {
        if (slot >= 0 && slot < onCounter.Length) onCounter[slot] = null;
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < Capacity && i < onCounter.Length; i++)
            if (onCounter[i] == null) return i;
        return -1;
    }

    private int CountOnCounter()
    {
        int n = 0;
        foreach (CounterItem ci in onCounter)
            if (ci != null) n++;
        return n;
    }
}
