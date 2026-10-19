using UnityEngine;

// El TPV del stand. Al llegar la camarera, abre el panel con los productos y la cuenta.
public class Tpv : Interactable
{
    [SerializeField] private TpvPanel panel;

    public override void Interact(WaitressController waitress)
    {
        if (panel != null) panel.Open(waitress);
    }

    // Mientras el panel esté abierto, la camarera espera en la TPV antes de seguir con la cola
    public override bool HoldsWaitress => panel != null && panel.IsOpen;

    public override void ReleaseWaitress()
    {
        if (panel != null && panel.IsOpen) panel.Close();
    }
}
