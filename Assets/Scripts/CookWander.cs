using UnityEngine;

// El cocinero: solo ambientación. Se mueve entre unos puntos de la barra al azar.
public class CookWander : MonoBehaviour
{
    [Tooltip("Puntos por los que se mueve (objetos vacíos detrás de la barra)")]
    [SerializeField] private Transform[] points;
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private float minWait = 0.5f;
    [SerializeField] private float maxWait = 2f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Si está activo, se voltea según hacia dónde camina. Desactivado: siempre mira igual (de espaldas a la barra)")]
    [SerializeField] private bool flipWithMovement = true;

    private Vector2 target;
    private float waitTimer;

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        PickNewTarget();
    }

    private void Update()
    {
        if (points == null || points.Length == 0) return;

        if (waitTimer > 0f)
        {
            waitTimer -= GameTime.Delta;
            if (waitTimer <= 0f) PickNewTarget();
            return;
        }

        Vector2 current = transform.position;
        transform.position = Vector2.MoveTowards(current, target, speed * GameTime.Delta);

        float dx = target.x - current.x;
        if (flipWithMovement && spriteRenderer != null && Mathf.Abs(dx) > 0.01f) spriteRenderer.flipX = dx < 0;

        if (((Vector2)transform.position - target).sqrMagnitude < 0.0001f)
            waitTimer = Random.Range(minWait, maxWait);
    }

    private void PickNewTarget()
    {
        if (points == null || points.Length == 0) return;
        target = points[Random.Range(0, points.Length)].position;
    }
}
