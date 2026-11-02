using UnityEngine;

// Comportamiento propio de un tipo de cliente (Pájaro azul, Hámster, Zorra...).
// Los tipos que no están en el catálogo (p. ej. la Loba) se comportan como un cliente base.
[System.Serializable]
public class CustomerTraits
{
    [Tooltip("Nombre del tipo, igual que en los Looks del CustomerSpawner (p. ej. 'Pájaro azul')")]
    public string typeName = "Cliente";

    [Header("Ira y paciencia")]
    [Tooltip("La ira que provoca se multiplica por esto (1.05 = un 5 % más, 0.95 = un 5 % menos)")]
    public float rageMultiplier = 1f;
    [Tooltip("Ritmo al que pierde la paciencia (1.05 = un 5 % más rápido, 0.95 = un 5 % más lento)")]
    public float patienceDrainMultiplier = 1f;

    [Header("Pedir más tiempo (al ir a tomar nota de la comida)")]
    [Tooltip("Probabilidad de que diga que aún no sabe qué pedir y vuelva a mirar la carta (una vez por visita)")]
    [Range(0f, 1f)] public float askMoreTimeChance = 0f;
    [Tooltip("Segundos que vuelve a mirar la carta antes de volver a llamar")]
    public float moreTimeSeconds = 4f;
    public string moreTimeComment = "¡Un momento, aún no sé qué pedir!";

    [Header("Al pagar")]
    [Tooltip("Propina extra: porcentaje de su parte de la cuenta, al azar entre mínimo y máximo (2 = 2 %)")]
    public float tipPercentMin = 0f;
    public float tipPercentMax = 0f;
    [Tooltip("Ira que baja al pagar, al azar entre mínimo y máximo")]
    public float rageReliefMin = 0f;
    public float rageReliefMax = 0f;

    [Header("Cambiar el pedido de comida")]
    [Tooltip("Probabilidad de que, poco después de pedir la comida, cambie de plato")]
    [Range(0f, 1f)] public float changeOrderChance = 0f;
    [Tooltip("Segundos después de tomar nota en los que cambia el pedido")]
    public float changeOrderDelay = 3f;
    public string changeOrderComment = "Me lo he pensado mejor...";
    [Tooltip("Si ha cambiado el pedido, al pagar deja este porcentaje de su parte de la cuenta como propina")]
    public float changeOrderTipPercent = 0f;

    [Header("Comentarios aleatorios")]
    [Tooltip("Frases propias. Si está vacío, usa las generales del CustomerCommentManager.")]
    public CustomerCommentEntry[] comments;
    [Tooltip("Ira que mueve cada comentario: los Annoying y Harassing la suben y los Positive la bajan. 0 = no afectan.")]
    public float commentRage = 0f;
}

// Catálogo de tipos de cliente (Assets/Resources/Clientes.asset).
// Si el archivo no existe, se usan los valores por defecto de Defaults().
[CreateAssetMenu(fileName = "Clientes", menuName = "The House Special/Catálogo de clientes")]
public class CustomerTraitsCatalog : ScriptableObject
{
    [Tooltip("Comportamiento de cada tipo de cliente. Los que no salen aquí son clientes base.")]
    public CustomerTraits[] types = Defaults();

    private static CustomerTraitsCatalog cached;

    public static CustomerTraitsCatalog Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<CustomerTraitsCatalog>("Clientes");
            if (cached == null) cached = CreateInstance<CustomerTraitsCatalog>();
            return cached;
        }
    }

    // Comportamiento de un tipo (null si es un cliente base)
    public static CustomerTraits Get(string typeName)
    {
        CustomerTraitsCatalog catalog = Instance;
        if (catalog.types == null || string.IsNullOrEmpty(typeName)) return null;
        foreach (CustomerTraits t in catalog.types)
            if (t != null && t.typeName == typeName) return t;
        return null;
    }

    // Valores del documento de diseño de los clientes
    public static CustomerTraits[] Defaults()
    {
        return new[]
        {
            new CustomerTraits
            {
                typeName = "Pájaro azul",
                rageMultiplier = 1.05f,
                patienceDrainMultiplier = 1.05f,
                askMoreTimeChance = 0.15f,
                moreTimeSeconds = 4f,
                moreTimeComment = "¡Un momento, aún no sé qué pedir!",
                commentRage = 2f,
                comments = new[]
                {
                    new CustomerCommentEntry("¿Hoy toca servicio lento o es siempre así?", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("¿Te pagan por estar parada?", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("Menudo antro.", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("Date prisa, que no tengo todo el día.", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("En mi nido esto no pasaba.", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("¿Sabes lo que es una propina? Pues olvídate.", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("Qué sitio más cutre.", CustomerCommentType.Harassing),
                    new CustomerCommentEntry("¡Pío, pío! ¡Que me muero de hambre!", CustomerCommentType.Harassing),
                },
            },
            new CustomerTraits
            {
                typeName = "Hámster",
                rageMultiplier = 0.95f,
                patienceDrainMultiplier = 0.95f,
                tipPercentMin = 2f,
                tipPercentMax = 10f,
                rageReliefMin = 1f,
                rageReliefMax = 5f,
                commentRage = 2f,
                comments = new[]
                {
                    new CustomerCommentEntry("¡Qué sitio tan bonito!", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Sin prisa, ¡lo estás haciendo genial!", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Muchas gracias por todo.", CustomerCommentType.Positive),
                    new CustomerCommentEntry("¡Qué bien huele aquí!", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Se nota que trabajas mucho.", CustomerCommentType.Positive),
                    new CustomerCommentEntry("¡Volveremos seguro!", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Qué amable eres.", CustomerCommentType.Positive),
                },
            },
            new CustomerTraits
            {
                typeName = "Zorra",
                changeOrderChance = 0.15f,
                changeOrderDelay = 3f,
                changeOrderComment = "Me lo he pensado mejor...",
                changeOrderTipPercent = 20f,
                commentRage = 2f,
                comments = new[]
                {
                    new CustomerCommentEntry("El servicio es impecable.", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Una atención muy correcta, sí señor.", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Todo está en su sitio. Me gusta.", CustomerCommentType.Positive),
                    new CustomerCommentEntry("Se agradece un servicio tan atento.", CustomerCommentType.Positive),
                    new CustomerCommentEntry("El servicio deja bastante que desear.", CustomerCommentType.Annoying),
                    new CustomerCommentEntry("Esperaba algo más de profesionalidad.", CustomerCommentType.Annoying),
                    new CustomerCommentEntry("Habría que revisar los tiempos de espera.", CustomerCommentType.Annoying),
                    new CustomerCommentEntry("Esta mesa no está a la altura.", CustomerCommentType.Annoying),
                },
            },
        };
    }
}
