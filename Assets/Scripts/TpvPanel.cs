using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// La pantalla del TPV.
// - Los botones de productos (se crean solos a partir de la carta) AÑADEN a una comanda.
// - "Pedir" manda toda la comanda a cocina y cierra el panel.
// - "Borrar" vacía la comanda. "Cuenta" saca una cuenta. "X" cierra sin pedir.
// Pon este script en el Canvas (que siempre está activo) y asigna el panel en "Root".
// Si no se asigna el botón "Pedir", funciona como antes: cada producto se pide al pulsarlo.
public class TpvPanel : MonoBehaviour
{
    [Header("Referencias de la UI")]
    [Tooltip("El panel que se muestra y se oculta")]
    [SerializeField] private GameObject root;
    [Tooltip("Objeto con un Grid Layout Group donde se crearán los botones")]
    [SerializeField] private Transform buttonContainer;
    [Tooltip("Prefab de botón (UI -> Button - TextMeshPro)")]
    [SerializeField] private Button productButtonPrefab;
    [SerializeField] private Button billButton;
    [SerializeField] private Button closeButton;
    [Tooltip("Manda la comanda a cocina")]
    [SerializeField] private Button orderButton;
    [Tooltip("Vacía la comanda (opcional)")]
    [SerializeField] private Button clearButton;
    [Tooltip("Texto que muestra lo que llevas apuntado en la comanda")]
    [SerializeField] private TMP_Text selectionText;
    [Tooltip("Texto opcional que muestra la ocupación de la barra, p. ej. 'Barra: 3/6'")]
    [SerializeField] private TMP_Text statusText;

    [Header("Botones con dibujo")]
    [Tooltip("Tamaño de cada botón cuando los productos tienen dibujo")]
    [SerializeField] private Vector2 rowButtonSize = new Vector2(160f, 100f);
    [Tooltip("Separación entre botones y entre filas")]
    [SerializeField] private float rowSpacing = 8f;
    [Tooltip("Cuánto se suben las filas para que no tapen el texto de la comanda")]
    [SerializeField] private float rowsRaise = 30f;

    private bool rowsReady;
    [Tooltip("Color de fondo de los botones con dibujo")]
    [SerializeField] private Color artButtonColor = new Color(0.93f, 0.9f, 0.84f);

    private WaitressController waitress;
    private readonly List<MenuItem> selection = new List<MenuItem>();

    private bool UsesOrderTicket => orderButton != null;

    public bool IsOpen => root != null && root.activeSelf;

    private void Start()
    {
        BuildButtons();
        if (billButton != null) billButton.onClick.AddListener(TakeBill);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (orderButton != null) orderButton.onClick.AddListener(SendOrder);
        if (clearButton != null) clearButton.onClick.AddListener(ClearSelection);
        if (Kitchen.Instance != null) Kitchen.Instance.OnMenuChanged += RebuildButtons;
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Kitchen.Instance != null) Kitchen.Instance.OnMenuChanged -= RebuildButtons;
    }

    private void BuildButtons()
    {
        if (Kitchen.Instance == null)
        {
            Debug.LogError("TpvPanel: no hay ninguna Kitchen en la escena.");
            return;
        }

        // Con dibujos: una fila con las bebidas y otra debajo con las comidas, centradas
        bool anyArt = false;
        foreach (MenuItem item in Kitchen.Instance.Menu)
            if (item != null && item.icon != null) anyArt = true;

        Transform drinkParent = buttonContainer, foodParent = buttonContainer;
        if (anyArt) SetupRows(out drinkParent, out foodParent);

        foreach (MenuItem item in Kitchen.Instance.Menu)
        {
            if (item == null) continue;
            Transform parent = item.category == ItemCategory.Drink ? drinkParent : foodParent;
            Button b = Instantiate(productButtonPrefab, parent);
            b.name = $"Btn_{item.displayName}";
            if (anyArt) ((RectTransform)b.transform).sizeDelta = rowButtonSize;

            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = item.displayName;

            if (item.icon != null)
            {
                // Con dibujo: el botón enseña la imagen en vez del nombre
                if (b.image != null) b.image.color = artButtonColor;
                if (label != null) label.gameObject.SetActive(false);
                AddIcon(b.transform, item.icon);
            }
            else if (b.image != null)
            {
                b.image.color = item.placeholderColor;
            }

            b.onClick.AddListener(() => OnProductPressed(item));
        }
    }

    // Cambia la rejilla de botones por dos filas centradas (bebidas arriba, comidas abajo)
    private void SetupRows(out Transform drinkRow, out Transform foodRow)
    {
        GridLayoutGroup grid = buttonContainer.GetComponent<GridLayoutGroup>();
        if (grid != null) DestroyImmediate(grid); // no puede haber dos "layouts" en el mismo objeto

        VerticalLayoutGroup column = buttonContainer.GetComponent<VerticalLayoutGroup>();
        if (column == null) column = buttonContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        column.childAlignment = TextAnchor.MiddleCenter;
        column.spacing = rowSpacing;
        column.childControlWidth = false;
        column.childControlHeight = false;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;

        if (!rowsReady)
        {
            ((RectTransform)buttonContainer).anchoredPosition += new Vector2(0f, rowsRaise);
            // El texto "Barra: 0/6 (+1 en la comanda)" en una sola línea, para que no baje encima de los botones
            if (statusText != null)
            {
                statusText.textWrappingMode = TextWrappingModes.NoWrap;
                statusText.rectTransform.sizeDelta = new Vector2(700f, statusText.rectTransform.sizeDelta.y);
            }
            rowsReady = true;
        }

        drinkRow = MakeRow("FilaBebidas");
        foodRow = MakeRow("FilaComidas");
    }

    private Transform MakeRow(string rowName)
    {
        var go = new GameObject(rowName, typeof(RectTransform));
        go.layer = buttonContainer.gameObject.layer;
        go.transform.SetParent(buttonContainer, false);
        ((RectTransform)go.transform).sizeDelta =
            new Vector2(((RectTransform)buttonContainer).rect.width, rowButtonSize.y);

        var row = go.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = rowSpacing;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return go.transform;
    }

    // Imagen del producto dentro del botón, ocupando casi todo el botón
    private static void AddIcon(Transform button, Sprite sprite)
    {
        var go = new GameObject("Icono", typeof(RectTransform));
        go.layer = button.gameObject.layer;
        go.transform.SetParent(button, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false; // el clic lo recoge el botón
    }

    // Cuando aparece un plato especial, se rehacen los botones
    private void RebuildButtons()
    {
        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            Destroy(buttonContainer.GetChild(i).gameObject);
        BuildButtons();
    }

    public void Open(WaitressController w)
    {
        waitress = w;
        selection.Clear();
        root.SetActive(true);
        RefreshTexts();
    }

    public void Close()
    {
        selection.Clear(); // lo que no se haya pedido se descarta
        root.SetActive(false);
    }

    private void Update()
    {
        if (!root.activeSelf) return;

        // Si la camarera se va del TPV, se cierra el panel
        if (waitress != null && waitress.IsMoving)
        {
            Close();
            return;
        }
        RefreshTexts();
    }

    // ------------------------------------------------------------- COMANDA

    private void OnProductPressed(MenuItem item)
    {
        if (!UsesOrderTicket)
        {
            // Modo antiguo: se pide al momento
            if (Kitchen.Instance.TryOrder(item))
                GameMessages.Show($"Pedido a cocina: {item.displayName}", 1.5f);
            return;
        }

        // No dejamos apuntar más de lo que cabe en la barra
        if (Kitchen.Instance.UsedCapacity + selection.Count >= Kitchen.Instance.Capacity)
        {
            GameMessages.Show("¡No cabe más en la barra!");
            return;
        }
        selection.Add(item);
        RefreshTexts();
    }

    private void SendOrder()
    {
        if (selection.Count == 0)
        {
            GameMessages.Show("La comanda está vacía: elige primero los productos");
            return;
        }

        var sent = new List<MenuItem>();
        foreach (MenuItem item in selection)
        {
            if (!Kitchen.Instance.TryOrder(item)) break; // la barra se ha llenado
            sent.Add(item);
        }

        if (sent.Count > 0) GameMessages.Show("Pedido a cocina: " + Describe(sent), 2f);
        Close();
    }

    private void ClearSelection()
    {
        selection.Clear();
        RefreshTexts();
    }

    private void TakeBill()
    {
        if (waitress != null && waitress.Hands.TryAdd(new HeldItem(HeldItemType.Bill)))
            GameMessages.Show("Sacas una cuenta", 1.5f);
    }

    // ------------------------------------------------------------- TEXTOS

    private void RefreshTexts()
    {
        if (statusText != null && Kitchen.Instance != null)
        {
            string extra = selection.Count > 0 ? $"  (+{selection.Count} en la comanda)" : "";
            statusText.text = $"Barra: {Kitchen.Instance.UsedCapacity}/{Kitchen.Instance.Capacity}{extra}";
        }

        if (selectionText != null)
            selectionText.text = selection.Count == 0
                ? "Pulsa los productos para apuntarlos en la comanda"
                : "Comanda: " + Describe(selection);

        if (orderButton != null) orderButton.interactable = selection.Count > 0;
    }

    // "2× Agua, Pollo asado"
    private static string Describe(List<MenuItem> items)
    {
        var counts = new List<KeyValuePair<MenuItem, int>>();
        foreach (MenuItem item in items)
        {
            int index = counts.FindIndex(p => p.Key == item);
            if (index >= 0) counts[index] = new KeyValuePair<MenuItem, int>(item, counts[index].Value + 1);
            else counts.Add(new KeyValuePair<MenuItem, int>(item, 1));
        }

        var sb = new StringBuilder();
        for (int i = 0; i < counts.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            if (counts[i].Value > 1) sb.Append(counts[i].Value).Append("× ");
            sb.Append(counts[i].Key.displayName);
        }
        return sb.ToString();
    }
}
