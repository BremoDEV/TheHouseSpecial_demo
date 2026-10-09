using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Detecta los clics del ratón y decide qué pasa:
// - Clic en un grupo de la cola -> lo selecciona (la camarera no se mueve).
// - Con un grupo seleccionado, clic en una mesa limpia y vacía -> se sientan solos.
// - Clic en un cliente sentado -> cuenta como clic en su mesa.
// - Clic en algo interactuable -> la camarera va y lo usa.
// - Clic en el suelo -> la camarera camina (y se cancela la selección).
// No hace nada si el juego está en pausa o la camarera se está llevando a alguien.
// Si la camarera está descansando, cualquier clic la saca del trastero.
public class ClickManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private WaitressController waitress;
    [Tooltip("Si se deja vacío, usa la Main Camera")]
    [SerializeField] private Camera cam;

    [Header("Opciones (editable por diseño)")]
    [Tooltip("Si está activo, hacer clic en el suelo vacío mueve a la camarera")]
    [SerializeField] private bool allowFloorMovement = true;

    [Tooltip("Margen del clic en unidades: si no aciertas justo, se coge lo clicable más cercano dentro de este radio")]
    [SerializeField] private float clickTolerance = 0.5f;

    private CustomerGroup selectedGroup;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (PauseMenu.IsPaused) return;
        if (DayManager.Instance != null && DayManager.Instance.IsDayOver) return;

        StoreroomManager storeroom = StoreroomManager.Instance;
        if (storeroom != null)
        {
            if (storeroom.InputLocked) return;          // se está llevando a un cliente
            if (storeroom.IsResting) storeroom.StopRest(); // sale del trastero y sigue con el clic
        }

        // Si el grupo seleccionado ya no está en la cola (se ha ido), lo olvidamos
        if (selectedGroup != null && selectedGroup.State != GroupState.InQueue)
            Deselect();

        Vector2 worldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Collider2D hit = PickCollider(worldPos);

        Interactable interactable = null;
        CustomerGroup group = hit != null ? hit.GetComponentInParent<CustomerGroup>() : null;

        if (group != null)
        {
            if (group.State == GroupState.InQueue)
            {
                Select(group);
                return;
            }
            if (group.AssignedTable == null) return; // se están yendo: ignorar
            interactable = group.AssignedTable;      // cliente sentado = su mesa
        }
        else if (hit != null)
        {
            interactable = hit.GetComponentInParent<Interactable>();
        }

        // ¿Estamos intentando sentar a un grupo?
        if (selectedGroup != null)
        {
            if (interactable is Table table)
            {
                if (table.IsAvailableForSeating)
                {
                    selectedGroup.SeatAt(table);
                    Deselect();
                }
                else
                {
                    Debug.Log($"{table.name} no está disponible ({table.State}).");
                    GameMessages.Show(table.State == TableState.Dirty ? "Esa mesa está sucia: recógela primero" : "Esa mesa está ocupada");
                }
                return;
            }
            Deselect(); // clic en otra cosa: cancelamos la selección y seguimos normal
        }

        if (interactable != null)
            waitress.GoInteract(interactable);
        else if (allowFloorMovement)
            waitress.MoveTo(worldPos);
    }

    // Elige qué se ha clicado, con margen de error:
    // 1) Busca todo lo que haya en un círculo alrededor del ratón (radio = clickTolerance).
    // 2) Entre los clientes y objetos clicables, se queda con el MÁS CERCANO al ratón
    //    (si dos están igual de cerca, gana el cliente).
    // 3) Si no hay nada clicable cerca, devuelve lo que haya justo debajo (pared, barra) o nada (suelo).
    private Collider2D PickCollider(Vector2 worldPos)
    {
        Collider2D best = null;
        float bestDistance = float.MaxValue;
        int bestScore = 0;

        foreach (Collider2D h in Physics2D.OverlapCircleAll(worldPos, clickTolerance))
        {
            int score = h.GetComponentInParent<CustomerGroup>() != null ? 2
                      : h.GetComponentInParent<Interactable>() != null ? 1 : 0;
            if (score == 0) continue;

            // 0 si el ratón está dentro del collider; si no, la distancia hasta su borde
            float distance = Vector2.Distance(h.ClosestPoint(worldPos), worldPos);
            bool closer = distance < bestDistance - 0.01f;
            bool tieButBetter = Mathf.Abs(distance - bestDistance) <= 0.01f && score > bestScore;
            if (closer || tieButBetter)
            {
                best = h;
                bestDistance = distance;
                bestScore = score;
            }
        }

        return best != null ? best : Physics2D.OverlapPoint(worldPos);
    }

    private void Select(CustomerGroup group)
    {
        Deselect();
        selectedGroup = group;
        selectedGroup.SetSelected(true);
    }

    private void Deselect()
    {
        if (selectedGroup != null) selectedGroup.SetSelected(false);
        selectedGroup = null;
    }
}
