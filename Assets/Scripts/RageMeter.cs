using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Barra de ira de la camarera.
// Cualquier script puede subirla con: RageMeter.Instance.AddRage(cantidad, grupoQueLaProvoca);
public class RageMeter : MonoBehaviour
{
    public static RageMeter Instance { get; private set; }

    [Header("Ira (editable por diseño)")]
    [Tooltip("Valor máximo de la barra")]
    [SerializeField] private float maxRage = 100f;

    [Tooltip("Ira actual (puedes cambiarla en Play para probar)")]
    [SerializeField] private float currentRage = 0f;

    [Header("UI (opcional)")]
    [Tooltip("Arrastra aquí un Slider de la interfaz para ver la barra")]
    [SerializeField] private Slider rageSlider;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando la barra se llena (aquí conectaremos el trastero)")]
    public UnityEvent onRageFull;

    // Para el tutorial: mientras sea true, AddRage no hace nada (SetRage y ReduceRage sí funcionan)
    public bool IgnoreRage { get; set; }

    public float Current => currentRage;
    public float Normalized => currentRage / maxRage;
    public bool IsFull => currentRage >= maxRage;

    // El último cliente que subió la ira: es el que te llevarás al trastero
    public CustomerGroup LastRageSource { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        UpdateUI();
    }

    public void AddRage(float amount, CustomerGroup source = null)
    {
        if (amount <= 0f || IsFull || IgnoreRage) return;

        currentRage = Mathf.Min(maxRage, currentRage + amount);
        if (source != null) LastRageSource = source;
        UpdateUI();

        if (IsFull)
        {
            Debug.Log($"¡IRA LLENA! Culpable: {(LastRageSource != null ? LastRageSource.name : "nadie")}");
            onRageFull?.Invoke();
        }
    }

    public void ReduceRage(float amount)
    {
        if (amount <= 0f) return;
        currentRage = Mathf.Max(0f, currentRage - amount);
        UpdateUI();
    }

    // Pone la ira a un valor concreto (se usa al salir del trastero)
    public void SetRage(float value)
    {
        currentRage = Mathf.Clamp(value, 0f, maxRage);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (rageSlider == null) return;
        rageSlider.maxValue = maxRage;
        rageSlider.value = currentRage;
    }
}
