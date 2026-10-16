using UnityEngine;

// La parte corta de la barra en L: aquí se deja todo lo que sobra.
// Platos sucios, productos equivocados o que ya nadie quiere, cartas, cubiertos y cuentas.
// No hay castigo: el único coste es el tiempo de venir hasta aquí.
public class DishReturn : Interactable
{
    public override void Interact(WaitressController waitress)
    {
        // Quita todo lo que lleve en las manos (la condición "h => true" acepta cualquier objeto)
        int count = waitress.Hands.RemoveAll(h => true);

        if (count == 0) GameMessages.Show("No llevas nada que dejar");
        else if (count == 1) GameMessages.Show("Dejas lo que llevabas en la barra");
        else GameMessages.Show($"Dejas {count} cosas en la barra");
    }
}
