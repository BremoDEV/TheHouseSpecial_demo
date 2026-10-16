using System.Collections.Generic;
using UnityEngine;

// La cola de la entrada. Coloca a los grupos en sus huecos
// y los hace avanzar cuando alguien sale de la cola.
public class CustomerQueue : MonoBehaviour
{
    [Tooltip("Huecos de la cola (objetos vacíos). El primero es el más cercano a las mesas.")]
    [SerializeField] private Transform[] slots;

    private readonly List<CustomerGroup> groups = new List<CustomerGroup>();

    public int Count => groups.Count;
    public bool IsFull => groups.Count >= slots.Length;

    public bool TryAdd(CustomerGroup group)
    {
        if (IsFull) return false;
        groups.Add(group);
        RefreshPositions();
        return true;
    }

    public void Remove(CustomerGroup group)
    {
        if (groups.Remove(group)) RefreshPositions();
    }

    private void RefreshPositions()
    {
        for (int i = 0; i < groups.Count; i++)
            groups[i].SetQueuePosition(slots[i].position);
    }
}
