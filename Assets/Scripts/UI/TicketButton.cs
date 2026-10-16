using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón con forma de comanda de papel (menú principal).
// Al pasar el ratón por encima se endereza y se agranda un poco; al pulsarlo "se aplasta".
// Solo responde donde el dibujo no es transparente (necesita Read/Write activado en el PNG).
[RequireComponent(typeof(Image))]
public class TicketButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Tamaño al pasar el ratón por encima (1 = normal)")]
    [SerializeField] private float hoverScale = 1.07f;
    [Tooltip("Tamaño mientras se mantiene pulsado")]
    [SerializeField] private float pressedScale = 0.96f;
    [Tooltip("Velocidad de la animación")]
    [SerializeField] private float speed = 12f;
    [Tooltip("Por debajo de esta transparencia (0-1) no se puede clicar (el hueco de la sombra)")]
    [SerializeField] private float alphaHitThreshold = 0.5f;

    private float restAngle;
    private bool hovered, pressed;

    private void Awake()
    {
        restAngle = transform.localEulerAngles.z;
        if (restAngle > 180f) restAngle -= 360f;

        // Que el clic solo cuente sobre el papel, no sobre la sombra (si el PNG lo permite)
        Image image = GetComponent<Image>();
        if (image.sprite != null && image.sprite.texture != null && image.sprite.texture.isReadable)
            image.alphaHitTestMinimumThreshold = alphaHitThreshold;
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;

    private void Update()
    {
        float targetScale = pressed ? pressedScale : hovered ? hoverScale : 1f;
        float targetAngle = hovered ? 0f : restAngle; // al pasar por encima se endereza
        float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, t);
        float angle = transform.localEulerAngles.z;
        if (angle > 180f) angle -= 360f;
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angle, targetAngle, t));
    }
}
