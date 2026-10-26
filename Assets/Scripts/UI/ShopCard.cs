using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una mejora en la carta de la tienda: dibujo en círculo, nombre, descripción, nivel, precio y botón Comprar.
// Los textos y precios salen del catálogo (Assets/Resources/Mejoras).
public class ShopCard : MonoBehaviour
{
    public UpgradeId upgrade;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyLabel;

    private ShopMenu shop;

    public void Init(ShopMenu owner)
    {
        shop = owner;
        if (buyButton != null) buyButton.onClick.AddListener(() => shop.Buy(upgrade));
    }

    public void Refresh()
    {
        UpgradeDef def = UpgradeCatalog.Instance != null ? UpgradeCatalog.Instance.Get(upgrade) : null;
        if (def == null) { gameObject.SetActive(false); return; }

        int level = RunState.Level(upgrade);
        bool maxed = level >= def.MaxLevel;

        if (nameText != null) nameText.text = def.displayName;
        if (descriptionText != null) descriptionText.text = def.description;

        if (icon != null)
        {
            // Enseña el dibujo del nivel que vas a comprar (o el último si ya está al máximo)
            Sprite s = def.IconFor(maxed ? level : level + 1);
            icon.sprite = s;
            icon.enabled = s != null;
            icon.preserveAspect = true;
        }

        if (levelText != null)
        {
            string next = maxed ? "Al máximo"
                : (def.levelEffects != null && level < def.levelEffects.Length && !string.IsNullOrEmpty(def.levelEffects[level])
                    ? "Siguiente: " + def.levelEffects[level] : "");
            levelText.text = def.MaxLevel > 1
                ? $"Nivel {level}/{def.MaxLevel}" + (next != "" ? "  ·  " + next : "")
                : (maxed ? "Comprado" : next);
        }

        if (buyButton != null)
        {
            int price = maxed ? 0 : def.prices[level];
            bool canPay = !maxed && RunState.Savings >= price;
            buyButton.interactable = canPay;
            if (buyLabel != null)
                buyLabel.text = maxed ? "Comprado" : (level == 0 ? $"Comprar · {price} €" : $"Mejorar · {price} €");
        }
    }
}
