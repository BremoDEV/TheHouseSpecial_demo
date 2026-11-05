using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Llamada del psiquiatra al final de cada día (después de la tienda).
// 1. El psiquiatra hace una pregunta con 4 respuestas: cuanto más calmada suena la respuesta, más fácil es el minijuego.
// 2. Minijuego de "mantener la calma" (estilo pesca): un pulso de pánico se mueve solo por la barra.
//    Manteniendo el clic (o espacio) la zona verde sube; al soltarlo, baja. Con el pulso dentro, la calma sube; fuera, baja.
//    Calma llena = llamada superada y al día siguiente. Calma a cero = la internan (game over).
// Cuantas más víctimas lleve la camarera en la partida (RunState.Kills), más difícil es.
public class PhoneCall : MonoBehaviour
{
    public const string SceneName = "Llamada";

    public enum AnswerTone { Calmada, Regular, Nerviosa }

    [System.Serializable]
    public class Answer
    {
        public string text;
        public AnswerTone tone;
        public Answer(string text, AnswerTone tone) { this.text = text; this.tone = tone; }
    }

    [System.Serializable]
    public class Question
    {
        [TextArea(1, 3)] public string text;
        public Answer[] answers = new Answer[4];
    }

    [Header("Preguntas (se elige una al azar en cada llamada)")]
    [SerializeField] private Question[] questions = DefaultQuestions();

    [Header("Frases del psiquiatra durante el minijuego")]
    [SerializeField] private string[] doctorLines =
    {
        "Respira hondo... ¿Me sigues?",
        "¿Has dormido bien esta semana?",
        "¿Ha pasado algo raro en el trabajo?",
        "Te noto un poco agitada.",
        "¿Sigues tomándote la medicación?",
        "Cuéntame cómo son tus clientes.",
        "¿Estás ahí? Te oigo respirar fuerte.",
        "Recuerda: si algo va mal, me llamas.",
    };
    [SerializeField] private float secondsPerLine = 6f;

    [Header("Dificultad")]
    [Tooltip("Cuánto sube la dificultad por cada víctima de la partida")]
    [SerializeField] private float difficultyPerKill = 0.35f;
    [Tooltip("Ajuste según la respuesta: calmada la baja, nerviosa la sube")]
    [SerializeField] private float calmAnswerBonus = -0.2f;
    [SerializeField] private float nervousAnswerPenalty = 0.3f;
    [Tooltip("Dificultad máxima (0 = día tranquilo)")]
    [SerializeField] private float maxDifficulty = 2f;

    [Header("Minijuego (valores con dificultad 0 → dificultad máxima)")]
    [Tooltip("Tamaño de la zona verde (parte de la barra)")]
    [SerializeField] private Vector2 zoneSize = new Vector2(0.3f, 0.15f);
    [Tooltip("Velocidad del pulso de pánico (barras por segundo)")]
    [SerializeField] private Vector2 pulseSpeed = new Vector2(0.25f, 0.8f);
    [Tooltip("Cada cuántos segundos cambia de rumbo el pulso")]
    [SerializeField] private Vector2 pulseRetarget = new Vector2(1.8f, 0.6f);
    [Tooltip("Calma que se pierde por segundo con el pulso fuera")]
    [SerializeField] private Vector2 calmDrain = new Vector2(0.03f, 0.08f);
    [Tooltip("Calma que se gana por segundo con el pulso dentro (unos 45 s de llamada)")]
    [SerializeField] private float calmFill = 0.03f;
    [SerializeField] private float startCalm = 0.5f;
    [Tooltip("Segundos al empezar en los que la calma no cambia, para coger el ritmo")]
    [SerializeField] private float graceSeconds = 2f;
    [Tooltip("Aceleración de la zona verde al mantener pulsado y gravedad al soltar")]
    [SerializeField] private float zoneLift = 2.6f;
    [SerializeField] private float zoneGravity = 1.9f;
    [SerializeField] private float zoneMaxSpeed = 1.3f;

    [Header("Escenas")]
    [SerializeField] private string menuScene = "MenuPrincipal";
    [SerializeField] private string fallbackNextDay = "Dia_2";

    [Header("Referencias de la UI")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text doctorText;
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TMP_Text[] answerLabels;
    [SerializeField] private GameObject minigamePanel;
    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform zone;
    [SerializeField] private RectTransform pulse;
    [SerializeField] private Image calmFillImage;
    [SerializeField] private TMP_Text calmText;
    [SerializeField] private RectTransform breathing; // provisional hasta tener la animación del trastero
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button resultButton;
    [SerializeField] private TMP_Text resultButtonLabel;

    private enum Phase { Question, Minigame, Result }
    private Phase phase;
    private float difficulty;   // 0 .. maxDifficulty
    private float zonePos;      // borde de abajo de la zona verde (0..1)
    private float zoneVel;
    private float pulsePos = 0.5f;
    private float pulseTarget = 0.5f;
    private float retargetTimer;
    private float calm;
    private float lineTimer;
    private int lineIndex;
    private float inputDelay;   // para que el clic de la respuesta no cuente en el minijuego
    private bool won;
    private float graceTimer;

    private float T => Mathf.Clamp01(difficulty / Mathf.Max(0.01f, maxDifficulty));

    private void Start()
    {
        Time.timeScale = 1f;
        int day = RunState.NextDayNumber;
        if (dayText != null) dayText.text = $"Noche antes del día {day}";
        ShowQuestion();
        if (resultButton != null) resultButton.onClick.AddListener(OnResultButton);
    }

    // ------------------------------------------------------------------ PREGUNTA

    private void ShowQuestion()
    {
        phase = Phase.Question;
        questionPanel.SetActive(true);
        minigamePanel.SetActive(false);
        resultPanel.SetActive(false);
        if (doctorText != null) doctorText.text = "Buenas noches. Como cada día, vamos a ver cómo estás.";

        Question q = questions != null && questions.Length > 0 ? questions[Random.Range(0, questions.Length)] : DefaultQuestions()[0];
        questionText.text = q.text;

        // Las respuestas se barajan para que la calmada no esté siempre en el mismo sitio
        var order = new List<Answer>(q.answers);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool used = i < order.Count && order[i] != null;
            answerButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            Answer a = order[i];
            answerLabels[i].text = a.text;
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => StartMinigame(a.tone));
        }
    }

    // ------------------------------------------------------------------ MINIJUEGO

    private void StartMinigame(AnswerTone tone)
    {
        float answerMod = tone == AnswerTone.Calmada ? calmAnswerBonus : tone == AnswerTone.Nerviosa ? nervousAnswerPenalty : 0f;
        difficulty = Mathf.Clamp(RunState.Kills * difficultyPerKill + answerMod, 0f, maxDifficulty);

        phase = Phase.Minigame;
        questionPanel.SetActive(false);
        minigamePanel.SetActive(true);
        calm = startCalm;
        zonePos = 0.5f - Size / 2f;
        zoneVel = 0f;
        pulsePos = 0.5f;
        pulseTarget = 0.5f;
        retargetTimer = 0f;
        lineTimer = 0f;
        lineIndex = Random.Range(0, Mathf.Max(1, doctorLines.Length));
        inputDelay = 0.4f;
        graceTimer = graceSeconds;
        SetZoneHeight();
        Debug.Log($"[Llamada] Víctimas: {RunState.Kills}, respuesta {tone} → dificultad {difficulty:0.00}");
    }

    private float Size => Mathf.Lerp(zoneSize.x, zoneSize.y, T);

    private void Update()
    {
        AnimateBreathing();
        if (phase != Phase.Minigame) return;
        float dt = Time.deltaTime;

        // Zona verde: sube con el clic o el espacio, baja por su peso
        inputDelay -= dt;
        bool holding = inputDelay <= 0f &&
                       ((Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                        (Keyboard.current != null && Keyboard.current.spaceKey.isPressed));
        zoneVel += (holding ? zoneLift : -zoneGravity) * dt;
        zoneVel = Mathf.Clamp(zoneVel, -zoneMaxSpeed, zoneMaxSpeed);
        zonePos += zoneVel * dt;
        float maxPos = 1f - Size;
        if (zonePos < 0f) { zonePos = 0f; zoneVel = 0f; }
        if (zonePos > maxPos) { zonePos = maxPos; zoneVel = 0f; }

        // Pulso de pánico: va a un punto al azar y cambia de rumbo cada poco (más a menudo y más rápido con más dificultad)
        retargetTimer -= dt;
        if (retargetTimer <= 0f || Mathf.Abs(pulsePos - pulseTarget) < 0.01f)
        {
            pulseTarget = Random.Range(0.05f, 0.95f);
            retargetTimer = Mathf.Lerp(pulseRetarget.x, pulseRetarget.y, T) * Random.Range(0.7f, 1.3f);
        }
        float speed = Mathf.Lerp(pulseSpeed.x, pulseSpeed.y, T);
        pulsePos = Mathf.MoveTowards(pulsePos, pulseTarget, speed * dt);
        // Temblor que crece con la dificultad
        float shake = Mathf.Sin(Time.time * 23f) * 0.012f * T;

        bool inside = pulsePos + shake >= zonePos && pulsePos + shake <= zonePos + Size;
        graceTimer -= dt;
        if (graceTimer <= 0f) calm += (inside ? calmFill : -Mathf.Lerp(calmDrain.x, calmDrain.y, T)) * dt;
        calm = Mathf.Clamp01(calm);

        // Pintar
        float h = track.rect.height;
        zone.anchoredPosition = new Vector2(0f, zonePos * h);
        pulse.anchoredPosition = new Vector2(0f, Mathf.Clamp01(pulsePos + shake) * h);
        var zoneImg = zone.GetComponent<Image>();
        if (zoneImg != null) zoneImg.color = inside ? new Color(0.45f, 0.82f, 0.5f) : new Color(0.3f, 0.6f, 0.36f);
        if (calmFillImage != null) calmFillImage.fillAmount = calm;
        if (calmText != null) calmText.text = $"Calma {Mathf.RoundToInt(calm * 100f)} %";

        // Frases del psiquiatra
        lineTimer -= dt;
        if (lineTimer <= 0f && doctorLines.Length > 0 && doctorText != null)
        {
            doctorText.text = doctorLines[lineIndex % doctorLines.Length];
            lineIndex++;
            lineTimer = secondsPerLine;
        }

        if (calm >= 1f) Finish(true);
        else if (calm <= 0f) Finish(false);
    }

    private void SetZoneHeight()
    {
        zone.sizeDelta = new Vector2(zone.sizeDelta.x, Size * track.rect.height);
    }

    // Respiración provisional (hasta tener la animación de la camarera en el trastero): más rápida cuanto más nerviosa
    private void AnimateBreathing()
    {
        if (breathing == null) return;
        float nervous = phase == Phase.Minigame ? (1f - calm) * 0.6f + T * 0.4f : 0.2f;
        float freq = Mathf.Lerp(0.8f, 3f, nervous);
        float s = 1f + Mathf.Sin(Time.time * freq * Mathf.PI) * Mathf.Lerp(0.02f, 0.06f, nervous);
        breathing.localScale = new Vector3(s, s, 1f);
    }

    // ------------------------------------------------------------------ RESULTADO

    private void Finish(bool success)
    {
        phase = Phase.Result;
        won = success;
        minigamePanel.SetActive(false);
        resultPanel.SetActive(true);
        if (success)
        {
            resultTitle.text = "Todo en orden";
            resultText.text = "\"Te noto bien. Descansa, y hablamos mañana.\"\n\nHas mantenido la calma.";
            resultButtonLabel.text = $"Empezar el día {RunState.NextDayNumber}";
            if (doctorText != null) doctorText.text = "Llamada finalizada.";
        }
        else
        {
            resultTitle.text = "Te han internado";
            resultText.text = "\"Algo no va bien. Mañana vendrán a buscarte.\"\n\nNo has conseguido mantener la calma.";
            resultButtonLabel.text = "Volver al menú";
            if (doctorText != null) doctorText.text = "...";
        }
    }

    private void OnResultButton()
    {
        if (won)
        {
            SceneManager.LoadScene(string.IsNullOrEmpty(RunState.NextDayScene) ? fallbackNextDay : RunState.NextDayScene);
        }
        else
        {
            RunState.Reset(); // game over: partida nueva desde el menú
            SceneManager.LoadScene(menuScene);
        }
    }

    // ------------------------------------------------------------------ PREGUNTAS POR DEFECTO

    public static Question[] DefaultQuestions()
    {
        Question Q(string text, string calm, string reg1, string reg2, string bad)
        {
            return new Question
            {
                text = text,
                answers = new[]
                {
                    new Answer(calm, AnswerTone.Calmada),
                    new Answer(reg1, AnswerTone.Regular),
                    new Answer(reg2, AnswerTone.Regular),
                    new Answer(bad, AnswerTone.Nerviosa),
                },
            };
        }
        return new[]
        {
            Q("¿Cómo te has sentido hoy?", "Me encuentro bastante bien.", "Voy tirando.", "Estoy cansada últimamente.", "Todo va fatal."),
            Q("¿Qué tal el trabajo en el restaurante?", "Bien, los clientes son agradables.", "Mucho trabajo, pero bien.", "Algunos clientes me sacan de quicio.", "No soporto a nadie."),
            Q("¿Estás durmiendo bien?", "Sí, duermo del tirón.", "Más o menos.", "Me cuesta dormirme.", "No pego ojo, oigo ruidos en el trastero."),
            Q("¿Te has tomado la medicación?", "Sí, todos los días.", "Casi siempre.", "Se me olvidó un día.", "¿Para qué? Estoy perfectamente."),
            Q("¿Has tenido pensamientos extraños?", "No, ninguno.", "Alguno, pero nada importante.", "Me distraigo con facilidad.", "Solo pienso en carne."),
        };
    }
}
