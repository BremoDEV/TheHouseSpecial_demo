using UnityEngine;

// Un montón de cartas o de cubiertos en el stand del TPV.
// Al llegar la camarera, coge uno (si tiene una mano libre).
public class ItemSource : Interactable
{
    [Tooltip("Qué da este objeto: Menu (carta) o Cutlery (cubiertos)")]
    [SerializeField] private HeldItemType itemType = HeldItemType.Menu;

    public override void Interact(WaitressController waitress)
    {
        if (itemType == HeldItemType.Product)
        {
            Debug.LogWarning($"{name}: ItemSource no sirve para productos, usa el TPV.");
            return;
        }

        if (waitress.Hands.TryAdd(new HeldItem(itemType)))
            Debug.Log($"Coges: {itemType}");
    }
}
