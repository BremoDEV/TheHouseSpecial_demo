using UnityEngine;

// La puerta del trastero. Al llegar la camarera, entra a descansar
// (la barra de ira baja mientras el restaurante sigue funcionando).
// También es el sitio al que lleva a los clientes cuando la ira se llena.
public class Storeroom : Interactable
{
    public override void Interact(WaitressController waitress)
    {
        if (StoreroomManager.Instance != null) StoreroomManager.Instance.StartRest();
    }

    // Mientras descansa, la camarera no sigue con la cola (sale con un clic o cuando la ira llega a 0)
    public override bool HoldsWaitress =>
        StoreroomManager.Instance != null && StoreroomManager.Instance.IsResting;

    public override void ReleaseWaitress()
    {
        if (StoreroomManager.Instance != null && StoreroomManager.Instance.IsResting)
            StoreroomManager.Instance.StopRest();
    }
}
