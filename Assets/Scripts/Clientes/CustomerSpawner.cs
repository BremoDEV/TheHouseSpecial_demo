using System.Collections.Generic;
using UnityEngine;

// Crea grupos de clientes y los mete en la cola.
// Regla: solo entra gente nueva si hay más mesas "libres" (sin nadie sentado,
// aunque estén sucias) que grupos esperando en la cola.
public class CustomerSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CustomerGroup groupPrefab;
    [SerializeField] private CustomerQueue queue;
    [Tooltip("Por dónde aparecen los clientes")]
    [SerializeField] private Transform entrancePoint;
    [Tooltip("Por dónde se van (puede ser el mismo que la entrada)")]
    [SerializeField] private Transform exitPoint;

    [Header("Ritmo de llegada (editable por diseño)")]
    [SerializeField] private float firstSpawnDelay = 2f;
    [SerializeField] private float minSpawnInterval = 10f;
    [SerializeField] private float maxSpawnInterval = 16f;
    [Tooltip("Probabilidad de que llegue una pareja en vez de una persona sola")]
    [Range(0f, 1f)] [SerializeField] private float chanceOfPair = 0.5f;

    [Header("Tipos de cliente (de momento solo cambia el aspecto)")]
    [SerializeField] private CustomerLook[] looks;

    [Tooltip("Desactívalo para que dejen de entrar clientes (p. ej. al acabar el día)")]
    public bool spawningEnabled = true;

    private List<Table> tables;
    private float timer;
    private int spawnedCount;

    private void Start()
    {
        tables = new List<Table>(FindObjectsByType<Table>());
        timer = firstSpawnDelay;
    }

    private void Update()
    {
        if (!spawningEnabled) return;
        if (DayManager.Instance != null && DayManager.Instance.IsClosed) return; // el restaurante ha cerrado

        timer -= GameTime.Delta;
        if (timer > 0f) return;

        if (CanSpawn())
        {
            Spawn();
            timer = Random.Range(minSpawnInterval, maxSpawnInterval);
        }
        else
        {
            timer = 0.5f; // vuelve a comprobar en medio segundo
        }
    }

    private bool CanSpawn()
    {
        return !queue.IsFull && queue.Count < CountFreeTables();
    }

    private int CountFreeTables()
    {
        int free = 0;
        foreach (Table t in tables)
            if (!t.HasPeople) free++;
        return free;
    }

    private void Spawn()
    {
        int size = Random.value < chanceOfPair ? 2 : 1;

        CustomerLook[] chosen = new CustomerLook[size];
        if (looks != null && looks.Length > 0)
            for (int i = 0; i < size; i++)
                chosen[i] = looks[Random.Range(0, looks.Length)];

        CreateGroup(size, chosen);
    }

    // Para el tutorial: crea YA un grupo concreto (tamaño y tipo de cliente por su nombre, p. ej. "Loba")
    public CustomerGroup SpawnNow(int size, string typeName)
    {
        CustomerLook look = null;
        if (looks != null)
            foreach (CustomerLook l in looks)
                if (l != null && l.typeName == typeName) look = l;

        CustomerLook[] chosen = new CustomerLook[size];
        for (int i = 0; i < size; i++) chosen[i] = look;
        return CreateGroup(size, chosen);
    }

    private CustomerGroup CreateGroup(int size, CustomerLook[] chosen)
    {
        CustomerGroup group = Instantiate(groupPrefab, entrancePoint.position, Quaternion.identity);
        spawnedCount++;
        group.name = $"Grupo_{spawnedCount}";
        group.Init(size, chosen, queue, exitPoint.position);
        queue.TryAdd(group);
        return group;
    }
}
