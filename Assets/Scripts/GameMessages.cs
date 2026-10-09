using TMPro;
using UnityEngine;

// Mensajes cortos en pantalla para que el jugador entienda qué pasa
// ("¡Barra llena!", "Esa mesa está sucia"...). Desde cualquier script:
//     GameMessages.Show("Texto");
// Va en un texto de la interfaz (abajo en el centro).
public class GameMessages : MonoBehaviour
{
    private static GameMessages instance;

    [SerializeField] private TMP_Text text;
    [Tooltip("Segundos que tarda en desvanecerse al final")]
    [SerializeField] private float fadeTime = 0.4f;

    private float timeLeft;

    private void Awake()
    {
        instance = this;
        if (text == null) text = GetComponent<TMP_Text>();
        if (text != null) text.alpha = 0f;
    }

    public static void Show(string message, float seconds = 2.5f)
    {
        if (instance == null || instance.text == null) return;
        instance.text.text = message;
        instance.text.alpha = 1f;
        instance.timeLeft = seconds;
    }

    private void Update()
    {
        if (text == null || timeLeft <= 0f) return;
        timeLeft -= Time.unscaledDeltaTime;
        text.alpha = Mathf.Clamp01(timeLeft / Mathf.Max(0.01f, fadeTime));
    }
}
