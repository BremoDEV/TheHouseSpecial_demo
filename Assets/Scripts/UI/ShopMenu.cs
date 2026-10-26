using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// La tienda entre días, como la carta de un restaurante (tríptico).
// Se gasta el dinero que te has pasado del objetivo (se acumula de un día a otro).
// Las mejoras duran hasta que te despiden o acabas la semana.
// Para probar en el editor: F9 da 100 € de ahorros.
public class ShopMenu : MonoBehaviour
{
    [SerializeField] private ShopCard[] cards;
    [SerializeField] private TMP_Text savingsText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;

    private void Start()
    {
        Time.timeScale = 1f;
        foreach (ShopCard c in cards) if (c != null) c.Init(this);
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (continueLabel != null) continueLabel.text = $"Empezar el día {RunState.NextDayNumber}";
        if (dayText != null) dayText.text = $"Antes del día {RunState.NextDayNumber}";
        RefreshAll();
    }

#if UNITY_EDITOR
    // Solo en el editor, para probar: F9 = +100 € de ahorros
    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame)
        {
            RunState.AddSavings(100f);
            RefreshAll();
        }
    }
#endif

    public void Buy(UpgradeId id)
    {
        if (RunState.TryBuy(id))
        {
            UpgradeDef def = UpgradeCatalog.Instance.Get(id);
            Debug.Log($"Comprado: {def.displayName} (nivel {RunState.Level(id)}). Quedan {RunState.Savings:0} €");
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (savingsText != null) savingsText.text = $"Ahorros: {RunState.Savings:0} €";
        foreach (ShopCard c in cards) if (c != null) c.Refresh();
    }

    private void Continue()
    {
        SceneManager.LoadScene(string.IsNullOrEmpty(RunState.NextDayScene) ? "Dia_1" : RunState.NextDayScene);
    }
}
