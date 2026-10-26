using UnityEngine;

// Estadísticas del día, para el ticket de fin de día.
// Cualquier script las apunta con DayStats.Algo(...) y el DayManager las enseña al acabar.
// Se ponen a cero al empezar cada día (DayManager.Awake).
public static class DayStats
{
    public static int CustomersServed { get; private set; }   // personas que han pagado
    public static int CustomersDineAndDash { get; private set; } // personas que se han ido de la mesa sin pagar
    public static int CustomersLeftQueue { get; private set; } // personas que se han cansado de esperar en la cola
    public static int CustomersKilled { get; private set; }   // personas que han "visitado" el trastero
    public static float Tips { get; private set; }
    public static float MoneyLost { get; private set; }       // lo que se ha perdido por simpas
    public static float MaxRage { get; private set; }         // 0..1
    public static int Rests { get; private set; }             // veces que ha entrado a descansar

    private static float patienceSum;
    private static int patienceSamples;

    // Paciencia que le quedaba al cliente cuando le atendiste (0..1, de media). 1 si no hay datos.
    public static float AveragePatience => patienceSamples > 0 ? patienceSum / patienceSamples : 1f;
    public static bool HasPatienceData => patienceSamples > 0;

    public static void Reset()
    {
        CustomersServed = 0;
        CustomersDineAndDash = 0;
        CustomersLeftQueue = 0;
        CustomersKilled = 0;
        Tips = 0f;
        MoneyLost = 0f;
        MaxRage = 0f;
        Rests = 0;
        patienceSum = 0f;
        patienceSamples = 0;
    }

    public static void Served(int people, float tip)
    {
        CustomersServed += people;
        Tips += tip;
    }

    public static void DineAndDash(int people, float lost)
    {
        CustomersDineAndDash += people;
        MoneyLost += lost;
    }

    public static void LeftQueue(int people) => CustomersLeftQueue += people;
    public static void Killed(int people) => CustomersKilled += people;
    public static void Rested() => Rests++;

    public static void RageLevel(float normalized) => MaxRage = Mathf.Max(MaxRage, normalized);

    public static void Patience(float normalized)
    {
        patienceSum += Mathf.Clamp01(normalized);
        patienceSamples++;
    }
}
