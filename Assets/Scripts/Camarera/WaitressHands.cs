using System;
using TMPro;
using UnityEngine;

public enum HeldItemType
{
    Product,     // una bebida o un plato
    Menu,        // la carta
    Cutlery,     // los cubiertos
    Bill,        // la cuenta
    DirtyDishes  // platos sucios de una mesa
}

// Algo que la camarera lleva en una mano
public class HeldItem
{
    public HeldItemType type;
    public MenuItem product; // solo se usa si type == Product

    public HeldItem(HeldItemType type, MenuItem product = null)
    {
        this.type = type;
        this.product = product;
    }

    public string DisplayName =>
        type == HeldItemType.Product && product != null ? product.displayName : type.ToString();
}

// Lo que lleva la camarera. Va en el mismo objeto que WaitressController.
// De base son 2 huecos (una cosa en cada mano). Con la mejora "Bandeja ampliada" pasan a 3 o 4
// y se ve una bandeja debajo de las cosas que lleva.
public class WaitressHands : MonoBehaviour
{
    // Máximo de huecos posible (con la bandeja a tope). Los que se usan de verdad: Capacity.
    public const int HandCount = 4;

    [Tooltip("Dos iconos encima de la camarera (mano izquierda y derecha)")]
    [SerializeField] private SpriteRenderer[] handIcons = new SpriteRenderer[HandCount];

    [Tooltip("Textos opcionales con el nombre de lo que lleva cada mano")]
    [SerializeField] private TMP_Text[] handLabels = new TMP_Text[HandCount];

    [Header("Iconos de los objetos que no son productos")]
    [SerializeField] private Sprite menuIcon;
    [SerializeField] private Color menuColor = new Color(0.9f, 0.6f, 0.2f);
    [SerializeField] private Sprite cutleryIcon;
    [SerializeField] private Color cutleryColor = new Color(0.75f, 0.75f, 0.8f);
    [SerializeField] private Sprite billIcon;
    [SerializeField] private Color billColor = new Color(0.25f, 0.5f, 0.95f);
    [SerializeField] private Sprite dirtyDishesIcon;
    [SerializeField] private Color dirtyDishesColor = new Color(0.45f, 0.35f, 0.25f);

    [Header("Tamaño de los dibujos en las manos")]
    [Tooltip("Tamaño (en unidades) de un objeto con dibujo en la mano. Sin dibujo se usa el cuadrado de siempre.")]
    [SerializeField] private float artSize = 0.6f;

    [Header("Bandeja (mejora de la tienda)")]
    [Tooltip("Separación entre las cosas de la bandeja cuando lleva 3 o 4")]
    [SerializeField] private float traySpacing = 0.5f;
    [Tooltip("Cuánto por debajo de las cosas va la bandeja")]
    [SerializeField] private float trayOffsetY = -0.32f;

    private readonly HeldItem[] slots = new HeldItem[HandCount];
    private Sprite[] defaultSprites;   // el cuadrado de cada mano, para volver a él
    private Vector3[] defaultScales;
    private SpriteRenderer trayRenderer;

    // Huecos que se pueden usar ahora mismo (2 de base, 3 o 4 con la bandeja)
    public int Capacity { get; private set; } = 2;

    public bool HasFreeHand
    {
        get
        {
            for (int i = 0; i < Capacity; i++)
                if (slots[i] == null) return true;
            return false;
        }
    }

    private void Awake()
    {
        Capacity = Mathf.Clamp(Upgrades.TrayCapacity, 1, HandCount);
        if (Capacity > 2) SetUpTray();

        defaultSprites = new Sprite[handIcons.Length];
        defaultScales = new Vector3[handIcons.Length];
        for (int i = 0; i < handIcons.Length; i++)
        {
            if (handIcons[i] == null) continue;
            defaultSprites[i] = handIcons[i].sprite;
            defaultScales[i] = handIcons[i].transform.localScale;
        }
    }

    private void Start()
    {
        Refresh();
    }

    public HeldItem Get(int hand) => slots[hand];

    public bool TryAdd(HeldItem item)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                Refresh();
                return true;
            }
        }
        Debug.Log("¡Manos llenas!");
        GameMessages.Show(Capacity > 2 ? "¡Bandeja llena! Deja algo primero" : "¡Manos llenas! Deja algo primero");
        return false;
    }

    public void RemoveAt(int hand)
    {
        slots[hand] = null;
        Refresh();
    }

    // Quita el primer objeto que cumpla la condición. Devuelve true si quitó algo.
    public bool RemoveFirst(Predicate<HeldItem> match)
    {
        for (int i = 0; i < HandCount; i++)
        {
            if (slots[i] != null && match(slots[i]))
            {
                RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    // Quita todos los objetos que cumplan la condición. Devuelve cuántos quitó.
    public int RemoveAll(Predicate<HeldItem> match)
    {
        int removed = 0;
        for (int i = 0; i < HandCount; i++)
        {
            if (slots[i] != null && match(slots[i]))
            {
                slots[i] = null;
                removed++;
            }
        }
        if (removed > 0) Refresh();
        return removed;
    }

    public void Refresh()
    {
        for (int i = 0; i < HandCount && i < handIcons.Length; i++)
        {
            SpriteRenderer r = handIcons[i];
            if (r == null) continue;

            HeldItem item = slots[i];
            r.enabled = item != null;
            Sprite icon = null;
            Color color = Color.white;
            if (item != null) GetLook(item, out icon, out color);

            // El nombre solo sale si no hay dibujo (p. ej. la cuenta); con dibujo basta el dibujo
            if (handLabels != null && i < handLabels.Length && handLabels[i] != null)
            {
                handLabels[i].enabled = item != null && icon == null;
                if (item != null) handLabels[i].text = LabelFor(item);
            }
            if (item == null) continue;

            if (icon != null)
            {
                // Dibujo: sin teñir y escalado para que mida 'artSize'
                r.sprite = icon;
                r.color = Color.white;
                float width = Mathf.Max(0.01f, icon.bounds.size.x);
                r.transform.localScale = Vector3.one * (artSize / width);
            }
            else
            {
                // Sin dibujo: el cuadrado de color de siempre
                if (defaultSprites != null && i < defaultSprites.Length && defaultSprites[i] != null) r.sprite = defaultSprites[i];
                if (defaultScales != null && i < defaultScales.Length) r.transform.localScale = defaultScales[i];
                r.color = color;
            }
        }
    }

    // Con la bandeja: crea los huecos que faltan (copiando la mano derecha), los pone en fila
    // y dibuja la bandeja debajo
    private void SetUpTray()
    {
        if (handIcons == null || handIcons.Length == 0 || handIcons[0] == null) return;

        var icons = new System.Collections.Generic.List<SpriteRenderer>(handIcons);
        var labels = new System.Collections.Generic.List<TMP_Text>(handLabels ?? new TMP_Text[0]);
        SpriteRenderer model = handIcons[handIcons.Length - 1] != null ? handIcons[handIcons.Length - 1] : handIcons[0];
        while (icons.Count < Capacity)
        {
            SpriteRenderer copy = Instantiate(model, model.transform.parent);
            copy.name = "Hand_" + (icons.Count + 1);
            icons.Add(copy);
            if (labels.Count > 0 && labels[labels.Count - 1] != null)
            {
                TMP_Text l = labels[labels.Count - 1];
                TMP_Text lc = Instantiate(l, l.transform.parent);
                lc.transform.localPosition = l.transform.localPosition + new Vector3(0f, 0.3f, 0f);
                labels.Add(lc);
            }
        }
        handIcons = icons.ToArray();
        handLabels = labels.ToArray();

        // En fila, centrados
        float y = handIcons[0].transform.localPosition.y;
        float startX = -(Capacity - 1) * traySpacing / 2f;
        for (int i = 0; i < Capacity; i++)
        {
            Vector3 p = handIcons[i].transform.localPosition;
            handIcons[i].transform.localPosition = new Vector3(startX + i * traySpacing, y, p.z);
        }

        // La bandeja (dibujo de la mejora, o una barra gris si no hay dibujo)
        UpgradeDef def = UpgradeCatalog.Instance != null ? UpgradeCatalog.Instance.Get(UpgradeId.Bandeja) : null;
        Sprite traySprite = def != null ? def.IconFor(RunState.Level(UpgradeId.Bandeja)) : null;
        var go = new GameObject("Bandeja");
        go.transform.SetParent(handIcons[0].transform.parent, false);
        go.transform.localPosition = new Vector3(0f, y + trayOffsetY, 0f);
        trayRenderer = go.AddComponent<SpriteRenderer>();
        trayRenderer.sortingOrder = handIcons[0].sortingOrder - 1;
        float width = Capacity * 0.4f + 0.1f; // 3 huecos = 1,3 unidades de ancho
        if (traySprite != null)
        {
            trayRenderer.sprite = traySprite;
            float s = width / Mathf.Max(0.01f, traySprite.bounds.size.x);
            go.transform.localScale = new Vector3(s, s, 1f);
        }
        else
        {
            trayRenderer.sprite = handIcons[0].sprite;
            trayRenderer.color = new Color(0.6f, 0.65f, 0.75f);
            go.transform.localScale = new Vector3(width, 0.12f, 1f);
        }
    }

    private static string LabelFor(HeldItem item)
    {
        switch (item.type)
        {
            case HeldItemType.Product: return item.product != null ? item.product.displayName : "?";
            case HeldItemType.Menu: return "Carta";
            case HeldItemType.Cutlery: return "Cubiertos";
            case HeldItemType.Bill: return "Cuenta";
            default: return "Sucios";
        }
    }

    private void GetLook(HeldItem item, out Sprite icon, out Color color)
    {
        switch (item.type)
        {
            case HeldItemType.Product:
                icon = item.product != null ? item.product.icon : null;
                color = item.product != null ? item.product.placeholderColor : Color.white;
                break;
            case HeldItemType.Menu: icon = menuIcon; color = menuColor; break;
            case HeldItemType.Cutlery: icon = cutleryIcon; color = cutleryColor; break;
            case HeldItemType.Bill: icon = billIcon; color = billColor; break;
            default: icon = dirtyDishesIcon; color = dirtyDishesColor; break;
        }
    }
}
