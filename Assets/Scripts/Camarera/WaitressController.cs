using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Controla el movimiento de la camarera.
// Funciona con una COLA DE ACCIONES: cada clic se apunta y la camarera las hace una detrás de otra
// (p. ej. clic en una mesa, luego en la TPV y luego en otra mesa = va a las tres en orden).
// Como máximo hay "maxQueuedActions" acciones a la vez (contando la que está haciendo).
// Encima de cada destino pendiente aparece su número (1, 2, 3).
// Clic derecho = cancelar la cola (se para donde esté).
// Si hay una WalkGrid en la escena, rodea los obstáculos siguiendo un camino de puntos.
[RequireComponent(typeof(WaitressHands))]
public class WaitressController : MonoBehaviour
{
    [Header("Movimiento (editable por diseño)")]
    [Tooltip("Velocidad de la camarera en unidades por segundo")]
    [SerializeField] private float moveSpeed = 5.5f;

    [Tooltip("Distancia a la que se considera que ha llegado al destino")]
    [SerializeField] private float arriveDistance = 0.05f;

    [Header("Cola de acciones (editable por diseño)")]
    [Tooltip("Cuántas acciones se pueden encadenar como máximo (contando la que está haciendo ahora)")]
    [Min(1)]
    [SerializeField] private int maxQueuedActions = 3;

    [Tooltip("Enseñar un número encima de cada destino pendiente")]
    [SerializeField] private bool showQueueNumbers = true;

    [Tooltip("Color de los números de la cola")]
    [SerializeField] private Color queueNumberColor = new Color(1f, 0.85f, 0.2f);

    [Tooltip("Tamaño de los números de la cola")]
    [SerializeField] private float queueNumberSize = 6f;

    [Tooltip("Altura del número sobre el punto de destino")]
    [SerializeField] private float queueNumberHeight = 0.45f;

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
    [Tooltip("Si el dibujo siempre mira al frente (sin animación), desmárcalo para que no se voltee")]
    [SerializeField] private bool flipWithMovement = true;

    // Una acción de la cola: ir a un punto del suelo, o ir a un objeto y usarlo
    private struct QueuedAction
    {
        public Interactable target; // null = solo caminar
        public Vector2 point;       // destino si es solo caminar
        public bool IsInteraction => target != null;
    }

    private readonly List<Vector2> path = new List<Vector2>(); // puntos que faltan por recorrer
    private readonly List<QueuedAction> queue = new List<QueuedAction>(); // acciones que esperan su turno
    private readonly List<TextMeshPro> markers = new List<TextMeshPro>();

    private bool isMoving;
    private bool hasCurrent;            // ¿está haciendo una acción ahora mismo?
    private QueuedAction current;       // la acción que está haciendo
    private Interactable holdingTarget; // objeto donde se ha quedado "retenida" (TPV abierta, descansando…)

    public bool IsMoving => isMoving;

    // Acciones que tiene pendientes (la actual + las que esperan)
    public int PendingActions => queue.Count + (hasCurrent ? 1 : 0);
    public int MaxQueuedActions => maxQueuedActions;

    // Velocidad real ahora mismo (la base + el extra por ira)
    public float CurrentSpeed => moveSpeed * (1f + GetRageBonus());
    public WaitressHands Hands { get; private set; }

    private void Awake()
    {
        Hands = GetComponent<WaitressHands>();
        if (Hands == null) Hands = gameObject.AddComponent<WaitressHands>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    // ------------------------------------------------------------------ COLA (clics del jugador)

    // Apunta "ir a este objeto y usarlo". Devuelve false si la cola está llena.
    public bool QueueInteract(Interactable target)
    {
        if (target == null) return false;
        return Enqueue(new QueuedAction { target = target });
    }

    // Apunta "caminar hasta este punto del suelo". Devuelve false si la cola está llena.
    public bool QueueMove(Vector2 destination)
    {
        return Enqueue(new QueuedAction { point = destination });
    }

    private bool Enqueue(QueuedAction action)
    {
        // Si se ha quedado esperando en algún sitio (TPV abierta, trastero), un clic nuevo la suelta
        ReleaseHold();

        // Doble clic en lo mismo: no lo apuntamos dos veces seguidas
        if (action.IsInteraction && LastAction(out QueuedAction last) && last.target == action.target)
            return true;

        if (PendingActions >= maxQueuedActions) return false;

        queue.Add(action);
        if (!hasCurrent) StartNext();
        RefreshMarkers();
        return true;
    }

    // Borra todas las acciones y la deja quieta donde esté
    public void ClearQueue()
    {
        ReleaseHold();
        queue.Clear();
        hasCurrent = false;
        path.Clear();
        isMoving = false;
        RefreshMarkers();
    }

    private bool LastAction(out QueuedAction last)
    {
        if (queue.Count > 0) { last = queue[queue.Count - 1]; return true; }
        if (hasCurrent) { last = current; return true; }
        last = default;
        return false;
    }

    private void StartNext()
    {
        hasCurrent = false;
        while (queue.Count > 0)
        {
            QueuedAction next = queue[0];
            queue.RemoveAt(0);

            // Si el objeto ha desaparecido mientras esperaba (p. ej. un plato que se ha puesto malo), lo saltamos
            if (next.IsInteraction && next.target == null) continue;
            if (next.IsInteraction && !next.target.isActiveAndEnabled) continue;

            current = next;
            hasCurrent = true;
            if (next.IsInteraction) SetDestination(next.target.InteractionPosition, true);
            else SetDestination(next.point, false); // si clicas en un obstáculo, va al punto libre más cercano

            // Ya estaba justo ahí: actuamos sin caminar
            if (!isMoving) OnArrived();
            break;
        }
        RefreshMarkers();
    }

    private void ReleaseHold()
    {
        if (holdingTarget == null) return;
        Interactable t = holdingTarget;
        holdingTarget = null;
        hasCurrent = false;
        if (t != null) t.ReleaseWaitress();
    }

    // ------------------------------------------------------------------ MOVIMIENTO DIRECTO
    // (lo usan las escenas guionizadas, como llevarse a un cliente al trastero: borra la cola)

    // Caminar a un punto del suelo sin hacer nada al llegar
    public void MoveTo(Vector2 destination)
    {
        ClearQueue();
        SetDestination(destination, false);
    }

    // Caminar hasta un objeto e interactuar con él al llegar (borra la cola)
    public void GoInteract(Interactable target)
    {
        ClearQueue();
        QueueInteract(target);
    }

    private void SetDestination(Vector2 destination, bool exact)
    {
        path.Clear();
        if (WalkGrid.Instance != null)
            path.AddRange(WalkGrid.Instance.FindPath(transform.position, destination, exact));
        else
            path.Add(destination); // sin rejilla: en línea recta
        isMoving = path.Count > 0;
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
        // Esperando en la TPV o en el trastero: en cuanto el objeto la suelte, sigue con la cola
        if (holdingTarget != null)
        {
            if (!holdingTarget.HoldsWaitress)
            {
                holdingTarget = null;
                StartNext();
            }
            return;
        }
        // Por si el objeto retenido se ha destruido
        if (hasCurrent && !isMoving && current.IsInteraction && current.target == null)
            StartNext();

        if (!isMoving) return;
        if (path.Count == 0) { isMoving = false; return; } // por seguridad (p. ej. tras recompilar en Play)

        Vector2 target = path[0]; // siguiente punto del camino
        Vector2 now = transform.position;
        Vector2 next = Vector2.MoveTowards(now, target, CurrentSpeed * Time.deltaTime);
        transform.position = next;

        float dx = target.x - now.x;
        if (flipWithMovement && spriteRenderer != null && Mathf.Abs(dx) > 0.01f)
            spriteRenderer.flipX = dx < 0;

        if (Vector2.Distance(next, target) <= arriveDistance)
        {
            transform.position = target;
            path.RemoveAt(0);
            if (path.Count == 0) // era el último punto: hemos llegado
            {
                isMoving = false;
                OnArrived();
            }
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
        // Movimiento directo (sin cola): no hay nada más que hacer
        if (!hasCurrent) { RefreshMarkers(); return; }

        QueuedAction done = current;
        if (done.IsInteraction && done.target != null)
        {
            done.target.Interact(this);

            // La interacción puede haber borrado la cola (p. ej. empieza a llevarse a un cliente)
            if (!hasCurrent) { RefreshMarkers(); return; }

            // TPV abierta, descansando…: espera aquí hasta que el objeto la suelte
            if (done.target != null && done.target.HoldsWaitress)
            {
                holdingTarget = done.target;
                RefreshMarkers();
                return;
            }
        }
        StartNext();
    }

    // ------------------------------------------------------------------ NÚMEROS DE LA COLA

    private void LateUpdate()
    {
        // Los destinos pueden moverse (o desaparecer), así que recolocamos los números cada frame
        if (markers.Count > 0) RefreshMarkers();
    }

    private void RefreshMarkers()
    {
        var points = new List<Vector2>();
        if (showQueueNumbers)
        {
            // La acción actual solo se marca mientras camina hacia ella
            if (hasCurrent && holdingTarget == null && isMoving && TryGetPoint(current, out Vector2 p)) points.Add(p);
            foreach (QueuedAction a in queue)
                if (TryGetPoint(a, out Vector2 q)) points.Add(q);
        }

        while (markers.Count < points.Count) markers.Add(CreateMarker());
        for (int i = 0; i < markers.Count; i++)
        {
            bool used = i < points.Count;
            markers[i].gameObject.SetActive(used);
            if (!used) continue;
            markers[i].text = (i + 1).ToString();
            markers[i].transform.position = new Vector3(points[i].x, points[i].y + queueNumberHeight, 0f);
        }
    }

    private static bool TryGetPoint(QueuedAction a, out Vector2 point)
    {
        if (a.IsInteraction)
        {
            point = a.target != null ? a.target.InteractionPosition : Vector2.zero;
            return a.target != null;
        }
        point = a.point;
        return true;
    }

    private TextMeshPro CreateMarker()
    {
        var go = new GameObject("NumeroCola");
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = queueNumberSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = queueNumberColor;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color32(40, 20, 40, 255);
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) r.sortingOrder = 500; // por encima de todo
        return tmp;
    }

    private void OnDestroy()
    {
        foreach (TextMeshPro m in markers)
            if (m != null) Destroy(m.gameObject);
    }
}

[System.Serializable]
public class RageSpeedStep
{
    [Range(0f, 1f)] public float rageAtLeast = 0.5f;
    [Tooltip("Extra total sobre la velocidad base: 0.1 = +10%")]
    public float totalBonus = 0.1f;
}
