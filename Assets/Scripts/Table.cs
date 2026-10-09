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

    public TableState State => state;
    public CustomerGroup Group { get; private set; }
    public bool HasPeople => Group != null;
    public bool IsAvailableForSeating => state == TableState.Empty && Group == null;

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
        Group = null;
        SetCutlery(false);
        SetState(leaveDirty ? TableState.Dirty : TableState.Empty);
    }

    public void SetCutlery(bool visible)
    {
        if (cutleryVisual != null) cutleryVisual.SetActive(visible);
    }

    public void SetState(TableState newState)
    {
        state = newState;
        UpdateVisual();
    }

    public void Clean()
    {
        SetState(TableState.Empty);
        Debug.Log($"{name}: mesa recogida.");
    }

    private void UpdateVisual()
    {
        if (tableRenderer == null) return;
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
