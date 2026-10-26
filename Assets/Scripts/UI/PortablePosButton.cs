using UnityEngine;
using UnityEngine.UI;

// Botón del HUD de la mejora "TPV portátil": abre la TPV desde donde esté la camarera.
// Solo se ve si se ha comprado la mejora. Al abrirla, la camarera se para (y se borra su cola de acciones).
public class PortablePosButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TpvPanel panel;
    [SerializeField] private WaitressController waitress;

    private void Start()
    {
        bool owned = Upgrades.HasPortablePos;
        gameObject.SetActive(owned);
        if (!owned) return;
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Open);
    }

    private void Open()
    {
        if (panel == null || waitress == null || panel.IsOpen) return;
        if (PauseMenu.IsPaused) return;
        if (DayManager.Instance != null && DayManager.Instance.IsDayOver) return;
        StoreroomManager sm = StoreroomManager.Instance;
        if (sm != null && (sm.InputLocked || sm.IsResting)) return;

        waitress.ClearQueue();
        panel.Open(waitress);
    }
}
