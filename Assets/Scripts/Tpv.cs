using UnityEngine;

// El TPV del stand. Al llegar la camarera, abre el panel con los productos y la cuenta.
public class Tpv : Interactable
{
    [SerializeField] private TpvPanel panel;

    public override void Interact(WaitressController waitress)
    {
        if (panel != null) panel.Open(waitress);
    }
}
