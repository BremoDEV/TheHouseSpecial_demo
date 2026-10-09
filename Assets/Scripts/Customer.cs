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

    [Header("Movimiento (editable por diseño)")]
    [SerializeField] private float walkSpeed = 3f;

    private readonly List<Vector2> path = new List<Vector2>();
    private bool hasTarget;

    public bool IsWalking => hasTarget;
    public string TypeName { get; private set; } = "Cliente";

    // Si es true, camina aunque el tiempo del juego esté congelado (cuando la camarera se lo lleva)
    public bool IgnoreFreeze { get; set; }

    public void SetLook(CustomerLook look)
    {
        if (look == null) return;
        TypeName = look.typeName;
        if (bodyRenderer != null)
        {
            if (look.sprite != null) bodyRenderer.sprite = look.sprite;
            bodyRenderer.color = look.tint;
        }
    }

    public void SetFaceColor(Color color)
    {
        if (faceRenderer != null) faceRenderer.color = color;
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
