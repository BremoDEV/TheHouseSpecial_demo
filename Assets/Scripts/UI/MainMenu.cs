using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú inicial.
// - Jugar: la primera vez lleva al tutorial; después de haberlo hecho (o saltado) una vez, va directo al día 1.
// - Tutorial: siempre abre el tutorial.
// Los nombres de las escenas tienen que coincidir con los de File → Build Profiles (Scene List).
public class MainMenu : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private string gameSceneName = "Dia_1";

    [Header("Botones")]
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private void Start()
    {
        Time.timeScale = 1f; // por si venimos de una partida pausada
        if (tutorialButton != null) tutorialButton.onClick.AddListener(() => SceneManager.LoadScene(tutorialSceneName));
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
    }

    // Clave que guarda si ya se ha visto el tutorial (la pone el TutorialManager al acabarlo o saltarlo)
    public const string TutorialSeenKey = "TutorialVisto";

    private void Play()
    {
        RunState.Reset(); // partida nueva: sin mejoras ni ahorros
        bool seen = PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
        SceneManager.LoadScene(seen ? gameSceneName : tutorialSceneName);
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
