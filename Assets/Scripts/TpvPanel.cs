using TMPro;
using UnityEngine;
using UnityEngine.UI;

// La pantalla del TPV: un botón por producto (se crean solos a partir de la carta
// de la cocina), un botón para sacar la cuenta y otro para cerrar.
// Pon este script en el Canvas (que siempre está activo) y asigna el panel en "Root".
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
    [Tooltip("Texto opcional que muestra la ocupación de la barra, p. ej. 'Barra: 3/6'")]
    [SerializeField] private TMP_Text statusText;

    private WaitressController waitress;

    private void Start()
    {
        BuildButtons();
        if (billButton != null) billButton.onClick.AddListener(TakeBill);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
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

            b.onClick.AddListener(() => Order(item));
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
        root.SetActive(true);
        RefreshStatus();
    }

    public void Close()
    {
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
        RefreshStatus();
    }

    private void Order(MenuItem item)
    {
        if (Kitchen.Instance.TryOrder(item))
            GameMessages.Show($"Pedido a cocina: {item.displayName}", 1.5f);
    }

    private void TakeBill()
    {
        if (waitress != null && waitress.Hands.TryAdd(new HeldItem(HeldItemType.Bill)))
            Debug.Log("TPV: sacas una cuenta");
    }

    private void RefreshStatus()
    {
        if (statusText == null || Kitchen.Instance == null) return;
        statusText.text = $"Barra: {Kitchen.Instance.UsedCapacity}/{Kitchen.Instance.Capacity}";
    }
}
