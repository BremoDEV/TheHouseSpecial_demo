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

// Las dos manos de la camarera. Va en el mismo objeto que WaitressController.
public class WaitressHands : MonoBehaviour
{
    public const int HandCount = 2;

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

    private readonly HeldItem[] slots = new HeldItem[HandCount];

    public bool HasFreeHand
    {
        get
        {
            foreach (HeldItem s in slots)
                if (s == null) return true;
            return false;
        }
    }

    private void Start()
    {
        Refresh();
    }

    public HeldItem Get(int hand) => slots[hand];

    public bool TryAdd(HeldItem item)
    {
        for (int i = 0; i < HandCount; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                Refresh();
                return true;
            }
        }
        Debug.Log("¡Manos llenas!");
        GameMessages.Show("¡Manos llenas! Deja algo primero");
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
            if (handLabels != null && i < handLabels.Length && handLabels[i] != null)
            {
                handLabels[i].enabled = item != null;
                if (item != null) handLabels[i].text = LabelFor(item);
            }
            if (item == null) continue;

            GetLook(item, out Sprite icon, out Color color);
            if (icon != null) r.sprite = icon;
            r.color = color;
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
