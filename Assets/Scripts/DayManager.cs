using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// El día de trabajo:
// - Un reloj de 12:00 a 20:00 que dura unos minutos reales.
// - Al llegar la hora de cierre: no entra nadie más, los de la cola se van
//   y los que ya están sentados terminan (tras la bebida piden directamente la cuenta).
// - Cuando no queda nadie, sale la pantalla de fin de día: ¿has llegado al mínimo de dinero?
public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    [Header("Duración del día (editable por diseño)")]
    [Tooltip("Segundos reales que dura el día. 480 = 8 minutos (1 hora del juego = 1 minuto)")]
    [SerializeField] private float dayDurationSeconds = 480f;
    [SerializeField] private int openingHour = 12;
    [SerializeField] private int closingHour = 20;
    [Tooltip("Al cerrar, los de la cola se van a casa (sin subir la ira)")]
    [SerializeField] private bool sendQueueHomeAtClosing = true;

    [Header("UI del reloj")]
    [SerializeField] private TMP_Text clockText;

    [Header("Pantalla de fin de día")]
    [Tooltip("Panel a pantalla completa (se desactiva solo al empezar)")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endTitleText;
    [SerializeField] private TMP_Text endDetailText;
    [SerializeField] private Button retryButton;

    public bool IsClosed { get; private set; }
    public bool IsDayOver { get; private set; }
    public int KillsToday { get; private set; }
    public float Progress => Mathf.Clamp01(elapsed / Mathf.Max(1f, dayDurationSeconds));

    private float elapsed;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;      // por si venimos de reiniciar con el juego pausado
        GameTime.Frozen = false;
        if (endPanel != null) endPanel.SetActive(false);
    }

    private void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        UpdateClock();
    }

    private void Update()
    {
        if (IsDayOver) return;

        if (!IsClosed)
        {
            elapsed += GameTime.Delta;
            UpdateClock();
            if (elapsed >= dayDurationSeconds) Close();
            return;
        }

        bool killing = StoreroomManager.Instance != null && StoreroomManager.Instance.IsKilling;
        if (!killing && NoCustomersLeft()) EndDay();
    }

    public void RegisterKill()
    {
        KillsToday++;
    }

    private void Close()
    {
        IsClosed = true;
        Debug.Log("¡Hora de cerrar! Ya no entra nadie más.");
        GameMessages.Show("¡20:00! El restaurante cierra", 4f);

        if (!sendQueueHomeAtClosing) return;
        foreach (CustomerGroup g in new List<CustomerGroup>(CustomerGroup.All))
            if (g != null) g.SendHome();
    }

    private bool NoCustomersLeft()
    {
        foreach (CustomerGroup g in CustomerGroup.All)
            if (g != null && g.State != GroupState.Leaving) return false;
        return true;
    }

    private void UpdateClock()
    {
        if (clockText == null) return;
        int startMinutes = openingHour * 60;
        int endMinutes = closingHour * 60;
        int now = Mathf.FloorToInt(Mathf.Lerp(startMinutes, endMinutes, Progress));
        clockText.text = $"{now / 60:00}:{now % 60:00}";
    }

    private void EndDay()
    {
        IsDayOver = true;
        Time.timeScale = 0f;

        float money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0f;
        float goal = MoneyManager.Instance != null ? MoneyManager.Instance.DailyGoal : 0f;
        bool success = money >= goal;

        Debug.Log($"Fin del día. Dinero: {money:0} / {goal:0}. Clientes en el trastero: {KillsToday}");

        if (endPanel != null) endPanel.SetActive(true);
        if (endTitleText != null)
            endTitleText.text = success ? "¡Día superado!" : "No has llegado al mínimo";
        if (endDetailText != null)
            endDetailText.text = $"Dinero: {money:0} € / {goal:0} €\nClientes que han visitado el trastero: {KillsToday}";
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
