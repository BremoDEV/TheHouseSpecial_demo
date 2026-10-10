using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú de pausa: botón en la esquina superior izquierda o tecla Escape.
// Para TODO el juego (Time.timeScale = 0).
// El botón "Salir" vuelve al menú principal.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Tooltip("Panel del menú de pausa (se desactiva solo al empezar)")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("El botón de pausa de la esquina")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Tooltip("Escena a la que lleva el botón Salir (tiene que estar en Build Profiles → Scene List)")]
    [SerializeField] private string mainMenuSceneName = "MenuPrincipal";

    private void Awake()
    {
        IsPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void Start()
    {
        if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (quitButton != null) quitButton.onClick.AddListener(GoToMainMenu);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (DayManager.Instance != null && DayManager.Instance.IsDayOver) return;
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void Restart()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        IsPaused = false;
        Time.timeScale = 1f;      // si no, el menú se quedaría congelado
        GameTime.Frozen = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
