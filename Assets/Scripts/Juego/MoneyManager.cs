using TMPro;
using UnityEngine;

// El dinero del día, los precios y las propinas.
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("Precios (editable por diseño)")]
    [Tooltip("De momento todas las bebidas valen lo mismo")]
    [SerializeField] private float drinkPrice = 3f;
    [Tooltip("De momento todos los platos valen lo mismo")]
    [SerializeField] private float foodPrice = 10f;

    [Header("Propinas (editable por diseño)")]
    [Tooltip("Desactívalo si las propinas desequilibran el juego")]
    [SerializeField] private bool tipsEnabled = true;
    [SerializeField] private float tipAmount = 2f;

    [Header("Objetivo del día")]
    [SerializeField] private float dailyGoal = 100f;

    [Header("Estado")]
    [SerializeField] private float money = 0f;

    [Header("UI (opcional)")]
    [SerializeField] private TMP_Text moneyText;

    public float Money => money;
    public float DailyGoal => dailyGoal;
    public bool GoalReached => money >= dailyGoal;
    public float TipAmount => tipsEnabled ? tipAmount : 0f;

    private void Awake()
    {
        Instance = this;
        UpdateUI();
    }

    public float PriceOf(MenuItem item)
    {
        float basePrice = item.category == ItemCategory.Drink ? drinkPrice : foodPrice;
        return basePrice * item.priceMultiplier; // el plato especial vale más
    }

    public void Add(float amount, string reason)
    {
        money += amount;
        Debug.Log($"{(amount >= 0 ? "+" : "")}{amount:0.##} € ({reason}). Total: {money:0.##} €");
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (moneyText != null) moneyText.text = $"{money:0} € / {dailyGoal:0} €";
    }
}
