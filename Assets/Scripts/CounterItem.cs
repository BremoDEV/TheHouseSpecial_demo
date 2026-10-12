using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Un producto ya preparado en la barra. Se puede recoger con clic
// y se pone malo (desaparece) si pasa demasiado tiempo.
public class CounterItem : Interactable
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [Tooltip("Texto opcional con el nombre del producto")]
    [SerializeField] private TMP_Text label;

    [Tooltip("OPCIÓN A: una Image en modo Filled/Radial dentro de un Canvas World Space (el círculo que se vacía)")]
    [SerializeField] private Image radialTimer;

    [Tooltip("OPCIÓN B (más fácil): un sprite rectangular hijo que se va encogiendo")]
    [SerializeField] private Transform timerBar;

    [Tooltip("Cuánto se agranda el producto cuando tiene dibujo (los dibujos llevan margen transparente)")]
    [SerializeField] private float artScale = 1.45f;

    // Para el tutorial: mientras sea true, nada se pone malo en la barra
    public static bool SpoilPaused { get; set; }

    private MenuItem item;
    private Kitchen kitchen;
    private int slot;
    private Vector2 standPosition;
    private float spoilTime;
    private float timeLeft;
    private Vector3 timerBarFullScale;

    public MenuItem Item => item;
    public override Vector2 InteractionPosition => standPosition;

    public void Setup(MenuItem item, Kitchen kitchen, int slot, Vector2 standPosition, float spoilTime)
    {
        this.item = item;
        this.kitchen = kitchen;
        this.slot = slot;
        this.standPosition = standPosition;
        this.spoilTime = Mathf.Max(0.1f, spoilTime);
        timeLeft = this.spoilTime;
        name = $"Barra_{item.displayName}";

        if (timerBar != null) timerBarFullScale = timerBar.localScale;
        if (label != null) label.text = item.displayName;

        if (iconRenderer != null)
        {
            if (item.icon != null) iconRenderer.sprite = item.icon;
            iconRenderer.color = item.IconColor;
        }

        // Con dibujo: más grande y sin el nombre encima (el dibujo ya dice qué es)
        if (item.icon != null)
        {
            transform.localScale *= artScale;
            if (label != null) label.gameObject.SetActive(false);
        }
        UpdateTimerVisual();
    }

    private void Update()
    {
        if (SpoilPaused) return;
        timeLeft -= GameTime.Delta;
        UpdateTimerVisual();

        if (timeLeft <= 0f)
        {
            Debug.Log($"{item.displayName} se ha puesto malo y se tira.");
            GameMessages.Show($"{item.displayName} se ha puesto malo");
            RemoveFromCounter();
        }
    }

    public override void Interact(WaitressController waitress)
    {
        if (waitress.Hands.TryAdd(new HeldItem(HeldItemType.Product, item)))
        {
            Debug.Log($"Recoges: {item.displayName}");
            RemoveFromCounter();
        }
    }

    private void UpdateTimerVisual()
    {
        float ratio = Mathf.Clamp01(timeLeft / spoilTime);
        if (radialTimer != null) radialTimer.fillAmount = ratio;
        if (timerBar != null)
            timerBar.localScale = new Vector3(timerBarFullScale.x * ratio, timerBarFullScale.y, timerBarFullScale.z);
    }

    private void RemoveFromCounter()
    {
        if (kitchen != null) kitchen.ClearSlot(slot);
        Destroy(gameObject);
    }
}
