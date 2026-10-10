using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú inicial: elegir entre hacer el tutorial o ir directamente a jugar.
// Los nombres de las escenas tienen que coincidir con los de File → Build Profiles (Scene List).
public class MainMenu : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private string gameSceneName = "Demo_Restaurante";

    [Header("Botones")]
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private void Start()
    {
        Time.timeScale = 1f; // por si venimos de una partida pausada
        if (tutorialButton != null) tutorialButton.onClick.AddListener(() => SceneManager.LoadScene(tutorialSceneName));
        if (playButton != null) playButton.onClick.AddListener(() => SceneManager.LoadScene(gameSceneName));
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
