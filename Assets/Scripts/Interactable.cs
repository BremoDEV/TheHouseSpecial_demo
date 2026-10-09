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
}
