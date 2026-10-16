using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Aspecto de un tipo de cliente. De momento los 4 tipos solo cambian el sprite/color.
[System.Serializable]
public class CustomerLook
{
    [Tooltip("Nombre del tipo, p. ej. 'Pájaro azul'. Se usa para el plato especial.")]
    public string typeName = "Cliente";
    [Tooltip("Sprite del cuerpo. Si se deja vacío, se mantiene el del prefab.")]
    public Sprite sprite;
    [Tooltip("Color del cuerpo (útil mientras usamos stickmans)")]
    public Color tint = Color.white;
}

// Una persona individual. Va dentro de un CustomerGroup (1 o 2 personas).
// Se encarga de caminar y de su aspecto (cuerpo, cara, bocadillos, marcador de selección).
public class Customer : MonoBehaviour
{
    [Header("Partes visuales")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("La cara cambia de color según la paciencia")]
    [SerializeField] private SpriteRenderer faceRenderer;

    [Tooltip("Bocadillo de aviso ('!', carta, cuenta)")]
    [SerializeField] private GameObject alertBubble;
    [Tooltip("Icono dentro del bocadillo de aviso")]
    [SerializeField] private SpriteRenderer alertIconRenderer;

    [Tooltip("Bocadillo con lo que ha pedido esta persona")]
    [SerializeField] private GameObject orderBubble;
    [Tooltip("Icono del producto dentro del bocadillo de pedido")]
    [SerializeField] private SpriteRenderer orderIconRenderer;

    [Tooltip("Texto opcional del aviso (PEDIR, CARTA, CUENTA)")]
    [SerializeField] private TMP_Text alertLabel;
    [Tooltip("Texto opcional con el nombre del producto pedido")]
    [SerializeField] private TMP_Text orderLabel;

    [Tooltip("Marca que se ve cuando el jugador selecciona al grupo en la cola")]
    [SerializeField] private GameObject selectionMarker;

    [Header("Dibujo del personaje (cuando el Look tiene sprite)")]
    [Tooltip("Alto total del dibujo en unidades (el PNG de 128x256 incluido el hueco transparente)")]
    [SerializeField] private float artHeight = 1.6f;
    [Tooltip("Cuánto se sube el bocadillo de pedido (la nube) para que no tape la cabeza")]
    [SerializeField] private float orderBubbleRaise = 0.3f;
    [Tooltip("Cuánto se sube el aviso (¡PEDIR!, CARTA, CUENTA)")]
    [SerializeField] private float alertBubbleRaise = 0.1f;
    [Tooltip("Cuánto se tiñe el dibujo de naranja/rojo cuando se impacienta (0 = nada, 1 = color puro)")]
    [Range(0f, 1f)]
    [SerializeField] private float moodTintStrength = 0.4f;

    [Header("Movimiento (editable por diseño)")]
    [SerializeField] private float walkSpeed = 3f;

    private readonly List<Vector2> path = new List<Vector2>();
    private bool hasTarget;

    private bool hasArt;

    public bool IsWalking => hasTarget;
    public string TypeName { get; private set; } = "Cliente";

    // Si es true, camina aunque el tiempo del juego esté congelado (cuando la camarera se lo lleva)
    public bool IgnoreFreeze { get; set; }

    private void Awake()
    {
        // Los bocadillos también se pueden clicar: cuentan como clic en el cliente,
        // y el ClickManager convierte "clic en cliente sentado" en "clic en su mesa".
        MakeClickable(alertBubble);
        MakeClickable(orderBubble);
    }

    // Pone un collider a cada dibujo del bocadillo (fondo e icono) para que el ratón lo detecte
    private static void MakeClickable(GameObject bubble)
    {
        if (bubble == null) return;
        foreach (SpriteRenderer sr in bubble.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.GetComponent<Collider2D>() != null) continue;
            BoxCollider2D col = sr.gameObject.AddComponent<BoxCollider2D>(); // se ajusta solo al tamaño del sprite
            col.isTrigger = true; // no empuja ni choca con nada, solo sirve para el clic
        }
    }

    public void SetLook(CustomerLook look)
    {
        if (look == null) return;
        TypeName = look.typeName;
        if (bodyRenderer == null) return;
        bodyRenderer.color = look.tint;
        if (look.sprite == null) return;

        // Con dibujo: tamaño real del personaje, sin la carita de color, y bocadillos un poco más arriba
        bodyRenderer.sprite = look.sprite;
        hasArt = true;
        float spriteHeight = Mathf.Max(0.01f, look.sprite.bounds.size.y);
        float scale = artHeight / spriteHeight;
        bodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        CapsuleCollider2D capsule = bodyRenderer.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            Vector2 size = look.sprite.bounds.size;
            capsule.size = new Vector2(size.x * 0.6f, size.y * 0.8f);
            capsule.offset = Vector2.zero;
        }
        if (faceRenderer != null) faceRenderer.gameObject.SetActive(false);
        Raise(alertBubble, alertBubbleRaise);
        Raise(orderBubble, orderBubbleRaise);
    }

    private static void Raise(GameObject bubble, float amount)
    {
        if (bubble == null) return;
        bubble.transform.localPosition += new Vector3(0f, amount, 0f);
    }

    // calm = paciencia alta. Con dibujo no hay carita: el personaje se tiñe un poco al enfadarse.
    public void SetFaceColor(Color color, bool calm = false)
    {
        if (faceRenderer != null) faceRenderer.color = color;
        if (hasArt && bodyRenderer != null)
            bodyRenderer.color = calm ? Color.white : Color.Lerp(Color.white, color, moodTintStrength);
    }

    public void SetAlert(bool visible, Sprite icon, Color color, string label = null)
    {
        if (alertBubble != null) alertBubble.SetActive(visible);
        if (!visible) return;
        if (alertLabel != null) alertLabel.text = label ?? "!";
        if (alertIconRenderer == null) return;
        if (icon != null) alertIconRenderer.sprite = icon;
        alertIconRenderer.color = color;
    }

    // null = ocultar el bocadillo de pedido
    public void SetOrder(MenuItem item)
    {
        if (orderBubble != null) orderBubble.SetActive(item != null);

        // Si el bocadillo tiene el efecto de distorsión por ira, él se encarga del icono
        OrderBubbleDistortion distortion = orderBubble != null ? orderBubble.GetComponent<OrderBubbleDistortion>() : null;
        if (distortion != null)
        {
            distortion.SetItem(item);
            return;
        }

        if (item == null) return;
        if (orderLabel != null) orderLabel.text = item.displayName;
        if (orderIconRenderer == null) return;
        if (item.icon != null) orderIconRenderer.sprite = item.icon;
        orderIconRenderer.color = item.placeholderColor;
    }

    public void SetSelected(bool selected)
    {
        if (selectionMarker != null) selectionMarker.SetActive(selected);
    }

    // Camina rodeando obstáculos (si hay WalkGrid)
    public void WalkTo(Vector2 destination)
    {
        path.Clear();
        if (WalkGrid.Instance != null)
            path.AddRange(WalkGrid.Instance.FindPath(transform.position, destination, true));
        else
            path.Add(destination);
        hasTarget = path.Count > 0;
    }

    // Camina en línea recta (para seguir a la camarera, que ya esquiva los obstáculos)
    public void WalkToDirect(Vector2 destination)
    {
        path.Clear();
        path.Add(destination);
        hasTarget = true;
    }

    public void StopWalking()
    {
        hasTarget = false;
        path.Clear();
    }

    private void Update()
    {
        if (!hasTarget) return;

        float dt = IgnoreFreeze ? Time.deltaTime : GameTime.Delta;
        Vector2 target = path[0];
        transform.position = Vector2.MoveTowards(transform.position, target, walkSpeed * dt);

        if (((Vector2)transform.position - target).sqrMagnitude < 0.0001f)
        {
            path.RemoveAt(0);
            if (path.Count == 0) hasTarget = false;
        }
    }
}
