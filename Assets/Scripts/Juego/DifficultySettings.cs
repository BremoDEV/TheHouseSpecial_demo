using UnityEngine;

// Ajustes de dificultad COMPARTIDOS por los 5 días (un solo archivo: Assets/Datos/Dificultad.asset).
// Cambias un número aquí y afecta a todos los días a la vez.
// Lo que cambia de un día a otro sale solo del número de día y de las mesas que tenga la escena.
[CreateAssetMenu(fileName = "Dificultad", menuName = "The House Special/Ajustes de dificultad")]
public class DifficultySettings : ScriptableObject
{
    [Header("Llegada de clientes (según las mesas)")]
    [Tooltip("Con este número de mesas se usa el intervalo tal cual. Con menos mesas llegan más despacio y con más, más rápido " +
             "(proporcional: con 3 mesas, 5/3 = 1,67 veces más tiempo entre grupos)")]
    [Min(1)] public int referenceTables = 5;
    [Tooltip("Segundos mínimos entre grupos (con 'Reference Tables' mesas y fuera de la hora punta)")]
    [Min(0.5f)] public float minSpawnInterval = 10f;
    [Tooltip("Segundos máximos entre grupos (con 'Reference Tables' mesas y fuera de la hora punta)")]
    [Min(0.5f)] public float maxSpawnInterval = 16f;
    [Tooltip("Probabilidad de que llegue una pareja en vez de una persona sola")]
    [Range(0f, 1f)] public float chanceOfPair = 0.5f;

    [Header("Hora punta")]
    [Tooltip("Hora a la que empieza (17.5 = 17:30)")]
    public float rushStartHour = 17.5f;
    [Tooltip("Hora a la que acaba (20 = 20:00, el cierre)")]
    public float rushEndHour = 20f;
    [Tooltip("Cuántas veces más rápido llegan los clientes en la hora punta (2 = el doble)")]
    [Min(1f)] public float rushMultiplier = 2f;
    [Tooltip("En la hora punta llega gente aunque todas las mesas estén ocupadas (hasta llenar la cola). " +
             "Fuera de la punta solo entra gente si hay mesa libre para ella.")]
    public bool rushIgnoresFreeTables = true;

    [Header("Paciencia de los clientes por día")]
    [Tooltip("El día que tiene la paciencia 'normal' (la que pone en el prefab del cliente)")]
    [Min(1)] public int referenceDay = 3;
    [Tooltip("Cuánto cambia la paciencia por cada día: 0.1 = día 1 120 %, día 2 110 %, día 3 100 %, día 4 90 %, día 5 80 %")]
    [Range(0f, 0.5f)] public float patienceChangePerDay = 0.1f;
    [Tooltip("Por si hay muchos días: la paciencia nunca baja de este %")]
    [Range(0.1f, 1f)] public float minPatience = 0.3f;

    [Header("Ira")]
    [Tooltip("Cuánto más rápido sube la ira por cada hora del día que ha pasado: 0.02 = +2 %/hora (a las 20:00, +16 %)")]
    [Range(0f, 0.2f)] public float rageIncreasePerHour = 0.02f;
    [Tooltip("Cuánto sube la ira cada día (1 = normal). Día 1 y 2 bajos para que casi nunca se llene; " +
             "a partir del día 3 lo normal es llevarse a 1 o 2 clientes al trastero")]
    public float[] rageByDay = { 0.6f, 0.8f, 1f, 1.1f, 1.2f };
    [Tooltip("La ira que dan los clientes de la COLA (enfadarse esperando o irse) se multiplica por esto. " +
             "0.5 = la mitad: sigue molestando, pero no es culpa tuya que no haya mesas")]
    [Range(0f, 1f)] public float queueRageScale = 0.5f;

    // Multiplicador del tiempo entre grupos según las mesas (más mesas = menos tiempo)
    public float SpawnIntervalScale(int tables) => referenceTables / (float)Mathf.Max(1, tables);

    public float PatienceMultiplier(int day) =>
        Mathf.Max(minPatience, 1f + (referenceDay - day) * patienceChangePerDay);

    public bool IsRushHour(float hour) => hour >= rushStartHour && hour < rushEndHour;

    public float RageDayMultiplier(int day)
    {
        if (rageByDay == null || rageByDay.Length == 0) return 1f;
        return rageByDay[Mathf.Clamp(day - 1, 0, rageByDay.Length - 1)];
    }
}
