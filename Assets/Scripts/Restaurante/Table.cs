using UnityEngine;

public enum TableState
{
    Empty,     // VACIA: limpia y sin nadie
    Occupied,  // OCUPADA: hay clientes sentados (o yendo a sentarse)
    Dirty      // SUCIA: los clientes se han ido, hay que recogerla
}

// Una mesa del restaurante (máximo 2 personas).
// - Libre para la cola = no hay nadie sentado (aunque esté sucia).
// - Solo se puede SENTAR a gente si está vacía y limpia.
// - Recoger una mesa sucia te pone los platos sucios en una mano.
public class Table : Interactable
{
    [Header("Mesa")]
    [Tooltip("Estado inicial (ponla en Dirty para probar la cola)")]
    [SerializeField] private TableState state = TableState.Empty;

    [Tooltip("Dónde se sientan los clientes (2 objetos vacíos hijos). " +
             "Si se dejan vacíos, se colocan a izquierda y derecha de la mesa.")]
    [SerializeField] private Transform[] seatPoints;
    [SerializeField] private float defaultSeatOffset = 0.7f;

    [Header("Visual temporal (mientras no hay sprites)")]
    [SerializeField] private SpriteRenderer tableRenderer;
    [Tooltip("Objeto hijo que se ve cuando la mesa está puesta (cubiertos)")]
    [SerializeField] private GameObject cutleryVisual;
    [SerializeField] private Color emptyColor = Color.white;
    [SerializeField] private Color occupiedColor = new Color(0.6f, 0.8f, 1f);
    [SerializeField] private Color dirtyColor = new Color(0.55f, 0.4f, 0.25f);
    [Tooltip("Si está activo, la mesa cambia de color según su estado (ocupada, sucia). Con el dibujo de la mesa va desactivado: la suciedad se ve por los platos sucios.")]
    [SerializeField] private bool tintByState = true;

    [Header("Platos y bebidas encima de la mesa")]
    [Tooltip("Cubiertos en servilleta: aparecen delante de cada cliente al poner la mesa")]
    [SerializeField] private Sprite cutlerySprite;
    [Tooltip("(No se usa de momento) Plato limpio")]
    [SerializeField] private Sprite cleanPlateSprite;
    [Tooltip("Plato sucio: lo que queda cuando terminan de comer o beber")]
    [SerializeField] private Sprite dirtyPlateSprite;
    [Tooltip("Tamaño (en unidades) de cada plato o bebida sobre la mesa")]
    [SerializeField] private float itemSize = 0.75f;
    [Tooltip("Dónde va la bebida de cada cliente (izquierda y derecha), respecto al centro de la mesa")]
    [SerializeField] private Vector2[] drinkSpots = { new Vector2(-0.42f, 0.16f), new Vector2(0.42f, 0.16f) };
    [Tooltip("Dónde va el plato de cada cliente (izquierda y derecha), respecto al centro de la mesa")]
    [SerializeField] private Vector2[] plateSpots = { new Vector2(-0.3f, -0.1f), new Vector2(0.3f, -0.1f) };
    [Tooltip("Dónde van los cubiertos de cada cliente (izquierda y derecha), por fuera del plato")]
    [SerializeField] private Vector2[] cutlerySpots = { new Vector2(-0.8f, -0.08f), new Vector2(0.8f, -0.08f) };
    [Tooltip("Tamaño (en unidades) de los cubiertos sobre la mesa")]
    [SerializeField] private float cutlerySize = 0.6f;

    private SpriteRenderer[] drinkSlots;
    private SpriteRenderer[] plateSlots;
    private SpriteRenderer[] cutlerySlots;

    public TableState State => state;
    public CustomerGroup Group { get; private set; }
    public bool HasPeople => Group != null;
    public bool IsAvailableForSeating => state == TableState.Empty && Group == null;

    private void Awake()
    {
        drinkSlots = CreateSlots("Bebida", drinkSpots, 2);
        plateSlots = CreateSlots("Plato", plateSpots, 3);
        cutlerySlots = CreateSlots("Cubiertos", cutlerySpots, 4);
    }

    private void Start()
    {
        SetCutlery(false);
        UpdateVisual();
    }

    public override void Interact(WaitressController waitress)
    {
        switch (state)
        {
            case TableState.Dirty:
                if (waitress.Hands.TryAdd(new HeldItem(HeldItemType.DirtyDishes)))
                    Clean();
                else
                    GameMessages.Show("Necesitas una mano libre para recoger la mesa");
                break;

            case TableState.Occupied:
                if (Group != null) Group.OnWaitressArrived(waitress);
                break;

            case TableState.Empty:
                Debug.Log($"{name}: mesa vacía, nada que hacer.");
                break;
        }
    }

    public Vector2 GetSeatPosition(int index)
    {
        if (seatPoints != null && index < seatPoints.Length && seatPoints[index] != null)
            return seatPoints[index].position;

        float side = index % 2 == 0 ? -1f : 1f;
        return (Vector2)transform.position + new Vector2(side * defaultSeatOffset, 0f);
    }

    public void AssignGroup(CustomerGroup group)
    {
        Group = group;
        SetState(TableState.Occupied);
    }

    public void ReleaseGroup(bool leaveDirty)
    {
        int people = Group != null ? Group.Size : 0;
        Group = null;
        if (cutleryVisual != null) cutleryVisual.SetActive(false);

        // Al irse: se quita todo y, si han tomado algo, queda un plato sucio por cliente
        ClearItems();
        if (leaveDirty && plateSlots != null)
            for (int i = 0; i < people && i < plateSlots.Length; i++)
                Show(plateSlots[i], dirtyPlateSprite, dirtyColor);

        SetState(leaveDirty ? TableState.Dirty : TableState.Empty);
    }

    // Poner la mesa. Con dibujo: unos cubiertos en servilleta delante de cada cliente; sin dibujo, la barrita gris.
    public void SetCutlery(bool visible)
    {
        bool useArt = visible && cutlerySprite != null && cutlerySlots != null;
        if (cutleryVisual != null) cutleryVisual.SetActive(visible && !useArt);
        if (!useArt) return;

        int people = Group != null ? Group.Size : cutlerySlots.Length;
        for (int i = 0; i < people && i < cutlerySlots.Length; i++)
            Show(cutlerySlots[i], cutlerySprite, Color.white, cutlerySize);
    }

    // ------------------------------------------------------------- PLATOS Y BEBIDAS

    // Al servir: el producto se queda en la mesa delante del cliente 'seat'
    public void ShowServed(int seat, MenuItem item)
    {
        SpriteRenderer[] slots = item.category == ItemCategory.Drink ? drinkSlots : plateSlots;
        if (slots == null || seat < 0 || seat >= slots.Length) return;
        Show(slots[seat], item.icon, item.IconColor);
    }

    // Al terminar de beber: las bebidas desaparecen sin más.
    // Al terminar de comer: la comida se queda hasta que se van (entonces sale el plato sucio).
    public void FinishConsuming(ItemCategory category)
    {
        if (category != ItemCategory.Drink || drinkSlots == null) return;
        foreach (SpriteRenderer r in drinkSlots) r.enabled = false;
    }

    private void ClearItems()
    {
        if (drinkSlots != null) foreach (SpriteRenderer r in drinkSlots) r.enabled = false;
        if (plateSlots != null) foreach (SpriteRenderer r in plateSlots) r.enabled = false;
        if (cutlerySlots != null) foreach (SpriteRenderer r in cutlerySlots) r.enabled = false;
    }

    // Enseña un dibujo (o un cuadrado de color si no hay dibujo) con el tamaño 'itemSize'
    private void Show(SpriteRenderer r, Sprite sprite, Color colorIfNoSprite, float size = -1f)
    {
        if (size <= 0f) size = itemSize;
        if (sprite != null)
        {
            r.sprite = sprite;
            r.color = Color.white;
            r.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, sprite.bounds.size.x));
        }
        else
        {
            r.sprite = squareSprite;
            r.color = colorIfNoSprite;
            r.transform.localScale = Vector3.one * size * 0.5f;
        }
        r.enabled = true;
    }

    private Sprite squareSprite; // el mismo cuadrado que usa la mesa, para cuando no hay dibujo

    private SpriteRenderer[] CreateSlots(string baseName, Vector2[] spots, int order)
    {
        if (tableRenderer != null) squareSprite = tableRenderer.sprite;
        var result = new SpriteRenderer[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            var go = new GameObject($"{baseName}_{i + 1}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = spots[i];
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            if (tableRenderer != null)
            {
                r.sharedMaterial = tableRenderer.sharedMaterial;
                r.sortingLayerID = tableRenderer.sortingLayerID;
            }
            r.enabled = false;
            result[i] = r;
        }
        return result;
    }

    public void SetState(TableState newState)
    {
        state = newState;
        UpdateVisual();
    }

    public void Clean()
    {
        ClearItems();
        SetState(TableState.Empty);
        Debug.Log($"{name}: mesa recogida.");
    }

    private void UpdateVisual()
    {
        if (tableRenderer == null) return;
        if (!tintByState)
        {
            tableRenderer.color = Color.white;
            return;
        }
        tableRenderer.color = state switch
        {
            TableState.Occupied => occupiedColor,
            TableState.Dirty => dirtyColor,
            _ => emptyColor
        };
    }

    private void OnValidate()
    {
        if (tableRenderer == null) tableRenderer = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    [ContextMenu("Prueba: ensuciar mesa")]
    private void DebugMakeDirty()
    {
        if (Group == null) SetState(TableState.Dirty);
    }
}
