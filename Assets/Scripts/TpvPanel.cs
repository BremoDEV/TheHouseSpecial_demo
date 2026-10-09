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

    private WaitressController waitress;
    private readonly List<MenuItem> selection = new List<MenuItem>();

    private bool UsesOrderTicket => orderButton != null;

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

        foreach (MenuItem item in Kitchen.Instance.Menu)
        {
            if (item == null) continue;
            Button b = Instantiate(productButtonPrefab, buttonContainer);
            b.name = $"Btn_{item.displayName}";

            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = item.displayName;

            if (b.image != null) b.image.color = item.placeholderColor;

            b.onClick.AddListener(() => OnProductPressed(item));
        }
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
