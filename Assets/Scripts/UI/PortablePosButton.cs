using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Botón del HUD de la mejora "TPV portátil": abre la TPV desde donde esté la camarera.
// Solo existe si se ha comprado la mejora. También se abre y se cierra con la barra espaciadora.
// Al abrirla, la camarera se queda quieta, pero conserva su cola de acciones y la sigue al cerrar.
public class PortablePosButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TpvPanel panel;
    [SerializeField] private WaitressController waitress;

    private bool ready;

    private void Start()
    {
        Refresh();
    }

    // Enseña u oculta el botón según si se tiene la mejora (también lo usa la herramienta de pruebas)
    public void Refresh()
    {
        bool owned = Upgrades.HasPortablePos;
        gameObject.SetActive(owned);
        if (!owned || ready) return;
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Toggle);
        if (panel != null) panel.RegisterToggleButton((RectTransform)transform);
        ready = true;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        // Que el botón no se quede seleccionado (si no, el espacio podría "pulsarlo" otra vez)
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (panel == null) return;

        if (panel.IsOpen) panel.Close();
        else Open();
    }

    private void Open()
    {
        if (waitress == null) return;
        if (PauseMenu.IsPaused) return;
        if (DayManager.Instance != null && DayManager.Instance.IsDayOver) return;
        StoreroomManager sm = StoreroomManager.Instance;
        if (sm != null && (sm.InputLocked || sm.IsResting)) return;

        panel.Open(waitress, true);
    }
}
