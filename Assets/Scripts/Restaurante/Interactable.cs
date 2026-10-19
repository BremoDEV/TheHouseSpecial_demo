using UnityEngine;

// Clase base para todo lo que la camarera puede "usar" al hacer clic:
// mesas, TPV, carta, cubiertos, platos de la barra, zona de platos sucios...
// Cada tipo concreto hereda de esta clase y decide qué pasa en Interact().
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Punto donde se colocará la camarera para interactuar (un hijo vacío). " +
             "Si se deja vacío, irá al centro del objeto.")]
    [SerializeField] private Transform interactionPoint;

    // Posición a la que debe caminar la camarera (algunos objetos la cambian)
    public virtual Vector2 InteractionPosition =>
        interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)transform.position;

    // Se llama cuando la camarera LLEGA al punto de interacción (no al hacer clic).
    public abstract void Interact(WaitressController waitress);

    // ¿La camarera tiene que quedarse aquí después de interactuar? (p. ej. con la TPV abierta
    // o descansando en el trastero). Mientras sea true, no pasa a la siguiente acción de la cola.
    public virtual bool HoldsWaitress => false;

    // Se llama si el jugador hace un clic nuevo mientras la camarera está "retenida" aquí:
    // el objeto debe soltarla (cerrar su panel, etc.) para que siga con la cola.
    public virtual void ReleaseWaitress() { }
}
