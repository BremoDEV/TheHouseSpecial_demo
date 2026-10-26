using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// El día de trabajo:
// - Un reloj de 12:00 a 20:00 que dura unos minutos reales.
// - Al llegar la hora de cierre: no entra nadie más, los de la cola se van
//   y los que ya están sentados terminan (tras la bebida piden directamente la cuenta).
// - Cuando no queda nadie, sale el TICKET de fin de día con las estadísticas:
//     · Si llegas al mínimo: "¡Has sobrevivido al día!" y botón Siguiente día
//       (en el último día: "¡Has sobrevivido a la semana!" y botón Menú principal).
//     · Si no llegas: sello de DESPEDIDA y se vuelve a empezar desde el día 1 (o al menú).
public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    [Header("Qué día es (editable por diseño)")]
    [Tooltip("Número de día que sale en el ticket")]
    [SerializeField] private int dayNumber = 1;
    [Tooltip("Escena del día siguiente. Vacío = es el último día (al superarlo se vuelve al menú)")]
    [SerializeField] private string nextDayScene = "";
    [Tooltip("Escena a la que se vuelve si fallas (empezar de nuevo)")]
    [SerializeField] private string firstDayScene = "Dia_1";
    [Tooltip("Escena del menú principal")]
    [SerializeField] private string menuScene = "MenuPrincipal";

    [Header("Dificultad (compartida por todos los días)")]
    [Tooltip("Assets/Datos/Dificultad: llegada de clientes por mesa, hora punta, paciencia por día e ira por hora")]
    [SerializeField] private DifficultySettings difficulty;

    [Header("Duración del día (editable por diseño)")]
    [Tooltip("Segundos reales que dura el día. 480 = 8 minutos (1 hora del juego = 1 minuto)")]
    [SerializeField] private float dayDurationSeconds = 480f;
    [SerializeField] private int openingHour = 12;
    [SerializeField] private int closingHour = 20;
    [Tooltip("Al cerrar, los de la cola se van a casa (sin subir la ira)")]
    [SerializeField] private bool sendQueueHomeAtClosing = true;

    [Header("UI del reloj")]
    [SerializeField] private TMP_Text clockText;

    [Header("Ticket de fin de día")]
    [Tooltip("Panel a pantalla completa (se desactiva solo al empezar)")]
    [SerializeField] private GameObject endPanel;
    [Tooltip("Título grande del ticket")]
    [SerializeField] private TMP_Text endTitleText;
    [Tooltip("Columna izquierda del ticket (los nombres de cada dato)")]
    [SerializeField] private TMP_Text endDetailText;
    [Tooltip("Columna derecha del ticket (los números)")]
    [SerializeField] private TMP_Text endValuesText;
    [Tooltip("Texto pequeño de arriba: 'DÍA 3'")]
    [SerializeField] private TMP_Text endDayText;
    [Tooltip("Fila del TOTAL (columna derecha)")]
    [SerializeField] private TMP_Text endTotalText;
    [Tooltip("Sello rojo de DESPEDIDA (solo sale si fallas)")]
    [SerializeField] private GameObject failStamp;
    [Tooltip("Botón principal: Siguiente día / Volver a empezar / Menú principal")]
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryButtonLabel;
    [Tooltip("Botón secundario: Menú (solo si fallas)")]
    [SerializeField] private Button menuButton;

    public bool IsClosed { get; private set; }
    public bool IsDayOver { get; private set; }
    public int KillsToday { get; private set; }
    public int DayNumber => dayNumber;
    public bool IsLastDay => string.IsNullOrEmpty(nextDayScene);
    public float Progress => Mathf.Clamp01(elapsed / Mathf.Max(1f, dayDurationSeconds));
    public DifficultySettings Difficulty => difficulty;

    // Hora del juego ahora mismo (12.5 = 12:30) y horas que han pasado desde la apertura
    public float CurrentHour => Mathf.Lerp(openingHour, closingHour, Progress);
    public float HoursElapsed => CurrentHour - openingHour;

    public bool IsRushHour => difficulty != null && !IsClosed && difficulty.IsRushHour(CurrentHour);

    // Paciencia de los clientes de este día (1 = la del prefab)
    public float PatienceMultiplier => difficulty != null ? difficulty.PatienceMultiplier(dayNumber) : 1f;

    // La ira sube más rápido según pasan las horas (1 = normal)
    public float RageMultiplier => difficulty != null ? 1f + difficulty.rageIncreasePerHour * HoursElapsed : 1f;

    private float elapsed;
    private bool success;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;      // por si venimos de reiniciar con el juego pausado
        GameTime.Frozen = false;
        DayStats.Reset();
        if (endPanel != null) endPanel.SetActive(false);
    }

    private void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnMainButton);
        if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
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

    // ------------------------------------------------------------------ FIN DEL DÍA

    private void EndDay()
    {
        IsDayOver = true;
        Time.timeScale = 0f;

        float money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0f;
        float goal = MoneyManager.Instance != null ? MoneyManager.Instance.DailyGoal : 0f;
        success = money >= goal;

        Debug.Log($"Fin del día {dayNumber}. Dinero: {money:0} / {goal:0}. " +
                  $"Atendidos: {DayStats.CustomersServed}. Trastero: {DayStats.CustomersKilled}");

        if (endPanel != null) endPanel.SetActive(true);

        if (endDayText != null) endDayText.text = $"DÍA {dayNumber}";
        if (endTitleText != null)
        {
            endTitleText.text = !success ? "No has llegado al mínimo"
                : IsLastDay ? "¡Felicidades! Has sobrevivido a la semana"
                : "¡Felicidades! Has sobrevivido al día";
        }
        if (failStamp != null) failStamp.SetActive(!success);

        FillTicket(money, goal);

        // Botones
        if (retryButtonLabel != null)
            retryButtonLabel.text = !success ? "Volver a empezar" : IsLastDay ? "Menú principal" : "Siguiente día";
        if (menuButton != null) menuButton.gameObject.SetActive(!success);
        // Con un solo botón, centrado; con dos, uno a cada lado
        if (retryButton != null && menuButton != null)
        {
            var rt = (RectTransform)retryButton.transform;
            float x = success ? 0f : -Mathf.Abs(((RectTransform)menuButton.transform).anchoredPosition.x);
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }
    }

    private void FillTicket(float money, float goal)
    {
        var labels = new List<string>();
        var values = new List<string>();

        void Line(string label, string value) { labels.Add(label); values.Add(value); }
        void Separator() { labels.Add("<color=#00000040>- - - - - - - - - - - - - - - - - - - - - - - -</color>"); values.Add(" "); }

        float diff = money - goal;
        Line("Caja del día", $"{money:0} €");
        Line("Objetivo", $"{goal:0} €");
        Line(diff >= 0f ? "Dinero extra" : "Te ha faltado", diff >= 0f ? $"+{diff:0} €" : $"{-diff:0} €");
        Line("Propinas", $"{DayStats.Tips:0} €");
        Line("Perdido en simpas", DayStats.MoneyLost > 0f ? $"-{DayStats.MoneyLost:0} €" : "0 €");
        Separator();
        Line("Clientes atendidos", DayStats.CustomersServed.ToString());
        Line("Se fueron sin pagar", DayStats.CustomersDineAndDash.ToString());
        Line("Se cansaron de esperar", DayStats.CustomersLeftQueue.ToString());
        Line("Visitaron el trastero", DayStats.CustomersKilled.ToString());
        Separator();
        Line("Paciencia media", DayStats.HasPatienceData ? $"{Mathf.RoundToInt(DayStats.AveragePatience * 100f)} %" : "-");
        Line("Ira máxima", $"{Mathf.RoundToInt(DayStats.MaxRage * 100f)} %");
        Line("Descansos en el trastero", DayStats.Rests.ToString());

        if (endDetailText != null) endDetailText.text = string.Join("\n", labels);
        if (endValuesText != null) endValuesText.text = string.Join("\n", values);
        if (endTotalText != null) endTotalText.text = $"{money:0} €";
    }

    private void OnMainButton()
    {
        Time.timeScale = 1f;
        if (!success) SceneManager.LoadScene(firstDayScene);  // despedida: vuelta al día 1
        else if (IsLastDay) SceneManager.LoadScene(menuScene); // fin de la semana
        else SceneManager.LoadScene(nextDayScene);
    }

    private void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    // Repetir este mismo día (lo usa el menú de pausa)
    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
