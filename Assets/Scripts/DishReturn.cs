using UnityEngine;

// La parte corta de la barra en L: aquí se dejan los platos sucios y desaparecen solos.
public class DishReturn : Interactable
{
    public override void Interact(WaitressController waitress)
    {
        int count = waitress.Hands.RemoveAll(h => h.type == HeldItemType.DirtyDishes);
        if (count > 0) Debug.Log($"Dejas {count} bandeja(s) de platos sucios.");
        else Debug.Log("No llevas platos sucios.");
    }
}
