using UnityEngine;

// Catálogo de la tienda de mejoras (Assets/Resources/Mejoras.asset). Ver Mejoras.cs.
[CreateAssetMenu(fileName = "Mejoras", menuName = "The House Special/Catálogo de mejoras")]
public class UpgradeCatalog : ScriptableObject
{
    [Tooltip("Las mejoras que salen en la tienda, en este orden")]
    public UpgradeDef[] upgrades;

    [Header("Valores de los efectos (índice 0 = sin mejora, 1 = nivel 1, 2 = nivel 2)")]
    [Tooltip("Bandeja ampliada: cuántas cosas lleva la camarera")]
    public int[] trayCapacity = { 2, 3, 4 };
    [Tooltip("Televisión: ritmo al que pierden paciencia los clientes SENTADOS (1 = normal)")]
    public float[] patienceDrainMult = { 1f, 0.9f, 0.8f };
    [Tooltip("Mostrador ampliado: huecos de la barra")]
    public int[] counterCapacity = { 2, 4, 6 };
    [Tooltip("Equipamiento de cocina: tiempo de preparación con la mejora (0.8 = un 20 % menos)")]
    public float cookTimeMult = 0.8f;
    [Tooltip("Zapatos cómodos: velocidad de la camarera con la mejora (1.15 = un 15 % más)")]
    public float moveSpeedMult = 1.15f;

    private static UpgradeCatalog cached;

    // Se carga solo desde Assets/Resources/Mejoras.asset
    public static UpgradeCatalog Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<UpgradeCatalog>("Mejoras");
            return cached;
        }
    }

    public UpgradeDef Get(UpgradeId id)
    {
        if (upgrades == null) return null;
        foreach (UpgradeDef u in upgrades)
            if (u != null && u.id == id) return u;
        return null;
    }
}
