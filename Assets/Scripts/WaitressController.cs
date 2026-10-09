using UnityEngine;

// Controla el movimiento de la camarera.
// Funciona por "último clic": si estaba yendo a un sitio y haces clic en otro,
// cambia de dirección al instante y olvida lo que iba a hacer.
[RequireComponent(typeof(WaitressHands))]
public class WaitressController : MonoBehaviour
{
    [Header("Movimiento (editable por diseño)")]
    [Tooltip("Velocidad de la camarera en unidades por segundo")]
    [SerializeField] private float moveSpeed = 5.5f;

    [Tooltip("Distancia a la que se considera que ha llegado al destino")]
    [SerializeField] private float arriveDistance = 0.05f;

    [Header("Velocidad extra por ira (editable por diseño)")]
    [Tooltip("Cada escalón: a partir de qué % de ira (0-1) y cuánto más rápida va EN TOTAL (0.1 = +10%)")]
    [SerializeField] private RageSpeedStep[] rageSpeedSteps =
    {
        new RageSpeedStep { rageAtLeast = 0.5f, totalBonus = 0.1f },
        new RageSpeedStep { rageAtLeast = 0.75f, totalBonus = 0.2f },
        new RageSpeedStep { rageAtLeast = 0.9f, totalBonus = 0.3f },
    };

    [Header("Referencias")]
    [Tooltip("Sprite de la camarera. Se voltea según la dirección. " +
             "Se asume que el dibujo original mira a la DERECHA.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Vector2 targetPosition;
    private bool isMoving;
    private Interactable pendingInteraction; // lo que hará al llegar (puede ser null)

    public bool IsMoving => isMoving;

    // Velocidad real ahora mismo (la base + el extra por ira)
    public float CurrentSpeed => moveSpeed * (1f + GetRageBonus());
    public WaitressHands Hands { get; private set; }

    private void Awake()
    {
        Hands = GetComponent<WaitressHands>();
        if (Hands == null) Hands = gameObject.AddComponent<WaitressHands>();
        targetPosition = transform.position;
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    // Caminar a un punto del suelo sin hacer nada al llegar
    public void MoveTo(Vector2 destination)
    {
        pendingInteraction = null;
        SetDestination(destination);
    }

    // Caminar hasta un objeto e interactuar con él al llegar
    public void GoInteract(Interactable target)
    {
        pendingInteraction = target;
        SetDestination(target.InteractionPosition);
    }

    private void SetDestination(Vector2 destination)
    {
        targetPosition = destination;
        isMoving = true;
    }

    // Muestra u oculta a la camarera (cuando descansa o durante la pantalla en negro)
    public void SetVisible(bool visible)
    {
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
            r.enabled = visible;
        if (visible) Hands.Refresh(); // vuelve a enseñar solo las manos que llevan algo
    }

    private void Update()
    {
        if (!isMoving) return;

        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, targetPosition, CurrentSpeed * Time.deltaTime);
        transform.position = next;

        float dx = targetPosition.x - current.x;
        if (spriteRenderer != null && Mathf.Abs(dx) > 0.01f)
            spriteRenderer.flipX = dx < 0;

        if (Vector2.Distance(next, targetPosition) <= arriveDistance)
        {
            transform.position = targetPosition;
            isMoving = false;
            OnArrived();
        }
    }

    private float GetRageBonus()
    {
        if (RageMeter.Instance == null || rageSpeedSteps == null) return 0f;
        float rage = RageMeter.Instance.Normalized;
        float bonus = 0f;
        foreach (RageSpeedStep step in rageSpeedSteps)
            if (rage >= step.rageAtLeast && step.totalBonus > bonus) bonus = step.totalBonus;
        return bonus;
    }

    private void OnArrived()
    {
        Interactable target = pendingInteraction;
        pendingInteraction = null;

        // Si el objeto ha desaparecido mientras íbamos (p. ej. un plato que se ha puesto malo), no hacemos nada
        if (target == null) return;

        target.Interact(this);
    }
}

[System.Serializable]
public class RageSpeedStep
{
    [Range(0f, 1f)] public float rageAtLeast = 0.5f;
    [Tooltip("Extra total sobre la velocidad base: 0.1 = +10%")]
    public float totalBonus = 0.1f;
}
