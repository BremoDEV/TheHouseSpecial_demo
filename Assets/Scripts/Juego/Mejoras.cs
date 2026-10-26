using System;
using UnityEngine;

// ===================================================================================
// TIENDA DE MEJORAS
// - UpgradeCatalog: el archivo con las 6 mejoras (Assets/Resources/Mejoras.asset): nombres, textos,
//   dibujos, precios por nivel y los valores de cada efecto. Todo editable en el Inspector.
// - RunState: lo que dura una partida (del día 1 al 5): ahorros y nivel de cada mejora.
//   Si te despiden o vuelves al menú, se borra (empiezas sin mejoras).
// - Upgrades: atajos para que el juego pregunte "¿cuánto vale ahora X?" (bandeja, velocidad…).
// ===================================================================================

public enum UpgradeId
{
    Bandeja = 0,      // más platos por viaje
    Television = 1,   // los clientes sentados pierden la paciencia más despacio
    TpvPortatil = 2,  // abrir la TPV desde cualquier sitio
    Mostrador = 3,    // más huecos en la barra
    Cocina = 4,       // el cocinero prepara más rápido
    Zapatos = 5,      // la camarera anda más rápido
}

[Serializable]
public class UpgradeDef
{
    public UpgradeId id;
    public string displayName = "Mejora";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Precio de cada nivel (el primero = nivel 1). El número de precios es el número de niveles.")]
    public int[] prices = { 50 };
    [Tooltip("Texto corto de lo que da cada nivel, p. ej. '3 platos' (mismo orden que los precios)")]
    public string[] levelEffects = { "" };
    [Tooltip("Dibujo de cada nivel (si solo hay uno, se usa para todos)")]
    public Sprite[] icons;

    public int MaxLevel => prices != null ? prices.Length : 0;

    public Sprite IconFor(int level)
    {
        if (icons == null || icons.Length == 0) return null;
        int i = Mathf.Clamp(level - 1, 0, icons.Length - 1);
        return icons[i];
    }
}

// Lo que dura una partida: ahorros para la tienda y nivel de cada mejora
public static class RunState
{
    private static readonly int[] levels = new int[Enum.GetValues(typeof(UpgradeId)).Length];

    public static float Savings { get; private set; }

    // A qué día se va al salir de la tienda
    public static string NextDayScene { get; set; } = "Dia_1";
    public static int NextDayNumber { get; set; } = 1;

    public static int Level(UpgradeId id) => levels[(int)id];

    public static void AddSavings(float amount)
    {
        if (amount > 0f) Savings += amount;
    }

    // Compra el siguiente nivel si hay dinero. Devuelve true si se ha comprado.
    public static bool TryBuy(UpgradeId id)
    {
        UpgradeDef def = UpgradeCatalog.Instance != null ? UpgradeCatalog.Instance.Get(id) : null;
        if (def == null) return false;
        int level = Level(id);
        if (level >= def.MaxLevel) return false;
        int price = def.prices[level];
        if (Savings < price) return false;
        Savings -= price;
        levels[(int)id] = level + 1;
        return true;
    }

    // Empezar de cero (partida nueva, despido o fin de la semana)
    public static void Reset()
    {
        Savings = 0f;
        for (int i = 0; i < levels.Length; i++) levels[i] = 0;
        NextDayScene = "Dia_1";
        NextDayNumber = 1;
    }
}

// Efectos de las mejoras, listos para usar desde cualquier script
public static class Upgrades
{
    private static UpgradeCatalog C => UpgradeCatalog.Instance;

    private static T Pick<T>(T[] values, UpgradeId id, T fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[Mathf.Clamp(RunState.Level(id), 0, values.Length - 1)];
    }

    public static int TrayCapacity => C != null ? Pick(C.trayCapacity, UpgradeId.Bandeja, 2) : 2;
    public static float PatienceDrainMult => C != null ? Pick(C.patienceDrainMult, UpgradeId.Television, 1f) : 1f;
    public static bool HasPortablePos => RunState.Level(UpgradeId.TpvPortatil) >= 1;
    public static int CounterCapacity => C != null ? Pick(C.counterCapacity, UpgradeId.Mostrador, 2) : 2;
    public static float CookTimeMult => RunState.Level(UpgradeId.Cocina) >= 1 && C != null ? C.cookTimeMult : 1f;
    public static float MoveSpeedMult => RunState.Level(UpgradeId.Zapatos) >= 1 && C != null ? C.moveSpeedMult : 1f;
}
