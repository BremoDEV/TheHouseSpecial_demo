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
        Collider2D hit = Physics2D.OverlapPoint(worldPos);

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
