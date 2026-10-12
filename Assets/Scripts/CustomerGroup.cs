using System.Collections.Generic;
using UnityEngine;

// IMPORTANTE: el orden de este enum importa (se usa para saber si están "sentados").
// Si añadís estados nuevos, ponedlos al final.
public enum GroupState
{
    InQueue,            // esperando en la cola                       (paciencia)
    WalkingToTable,     // caminando a la mesa
    Seated,             // recién sentados, pensando
    WantsToOrderDrinks, // '!' para pedir bebida                      (paciencia)
    WaitingForDrinks,   // ya has tomado nota, esperan las bebidas    (paciencia)
    Drinking,           // bebiendo
    WantsMenu,          // piden la carta                             (paciencia)
    ChoosingFood,       // mirando la carta
    WantsToOrderFood,   // '!' para pedir comida                      (paciencia)
    WaitingForFood,     // esperan la comida                          (paciencia)
    Eating,             // comiendo
    WantsBill,          // piden la cuenta                            (paciencia)
    Leaving,            // se van
    BeingTaken          // la camarera se los lleva al trastero
}

public enum CustomerMood
{
    Happy,    // paciencia por encima del 50%
    Annoyed,  // entre 50% y 25%: sarcástico, sube un poco la ira
    Angry     // por debajo del 25%: se queja, sube más la ira
}

public enum AlertType { None, Order, Menu, Bill }

// Un grupo de 1 o 2 clientes que llegan, esperan, se sientan, consumen y se van juntos.
// La paciencia es compartida y se RESETEA al empezar cada fase de espera.
public class CustomerGroup : MonoBehaviour
{
    // Lista de todos los grupos que hay ahora mismo en la escena
    public static readonly List<CustomerGroup> All = new List<CustomerGroup>();

    [Header("Miembros (los 2 hijos con Customer del prefab)")]
    [SerializeField] private Customer[] members;
    [Tooltip("Separación entre los dos clientes cuando esperan en la cola")]
    [SerializeField] private Vector2 queueMemberOffset = new Vector2(0.6f, 0f);

    [Header("Paciencia por fase, en segundos hasta irse (editable por diseño)")]
    [SerializeField] private float queuePatience = 50f;
    [SerializeField] private float orderDrinksPatience = 30f;
    [SerializeField] private float waitDrinksPatience = 50f;
    [SerializeField] private float menuPatience = 30f;
    [SerializeField] private float orderFoodPatience = 30f;
    [SerializeField] private float waitFoodPatience = 60f;
    [SerializeField] private float billPatience = 30f;
    [Range(0f, 1f)] [SerializeField] private float annoyedThreshold = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float angryThreshold = 0.25f;

    [Header("Tiempos sin paciencia, en segundos (editable por diseño)")]
    [SerializeField] private float thinkingTime = 3f;
    [SerializeField] private float drinkingTime = 6f;
    [SerializeField] private float choosingTime = 4f;
    [SerializeField] private float eatingTime = 8f;
    [Tooltip("Probabilidad de que, tras beber, pidan la carta (si no, piden la cuenta)")]
    [Range(0f, 1f)] [SerializeField] private float chanceToOrderFood = 0.6f;

    [Header("Ira por humor (editable por diseño)")]
    [SerializeField] private float rageOnAnnoyed = 3f;
    [SerializeField] private float rageOnAngry = 6f;
    [SerializeField] private float ragePerSecondAnnoyed = 0.2f;
    [SerializeField] private float ragePerSecondAngry = 0.6f;
    [Tooltip("Los enfados en la cola cuentan menos (0.25 = una cuarta parte)")]
    [Range(0f, 1f)] [SerializeField] private float queueRageMultiplier = 0.25f;

    [Header("Ira al irse sin pagar (editable por diseño)")]
    [Tooltip("Si se van desde la cola (sin haber consumido): poquita")]
    [SerializeField] private float rageOnLeaveQueue = 3f;
    [Tooltip("Ira base si se van desde la mesa")]
    [SerializeField] private float rageOnLeaveTable = 10f;
    [Tooltip("Ira extra por cada euro que pierdes")]
    [SerializeField] private float ragePerMoneyLost = 1f;

    [Header("Equivocarse de pedido (editable por diseño)")]
    [Tooltip("Parte de la paciencia de la fase que se pierde (0.2 = 20%)")]
    [Range(0f, 1f)] [SerializeField] private float wrongItemPatienceLoss = 0.2f;
    [SerializeField] private float wrongItemRage = 5f;

    [Header("Iconos de aviso (si no hay sprite, se usa el color)")]
    [SerializeField] private Sprite orderAlertIcon;
    [SerializeField] private Color orderAlertColor = Color.yellow;
    [SerializeField] private Sprite menuAlertIcon;
    [SerializeField] private Color menuAlertColor = new Color(0.9f, 0.6f, 0.2f);
    [SerializeField] private Sprite billAlertIcon;
    [SerializeField] private Color billAlertColor = new Color(0.25f, 0.5f, 0.95f); // azul, como el botón Cuenta del TPV

    [Header("Colores de la cara")]
    [SerializeField] private Color happyColor = new Color(0.4f, 0.85f, 0.4f);
    [SerializeField] private Color annoyedColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color angryColor = new Color(0.95f, 0.25f, 0.2f);

    public GroupState State { get; private set; }
    public CustomerMood Mood { get; private set; }
    public Table AssignedTable { get; private set; }
    public int Size { get; private set; }
    public bool HasCutlery { get; private set; }
    public bool HasConsumedSomething { get; private set; }
    public float PatienceNormalized => phaseDuration > 0f ? patienceLeft / phaseDuration : 1f;
    public bool CanBeTaken => State != GroupState.BeingTaken;
    public bool IsSelected { get; private set; }

    // Para el tutorial: si es true, la paciencia no baja (la cara se queda verde)
    public bool PatiencePaused { get; set; }
    // Para el tutorial: true = tras beber piden la carta, false = la cuenta, null = al azar
    public bool? ForcedFoodChoice { get; set; }

    // El primer cliente del grupo (para señalarlo con la flecha del tutorial)
    public Transform Anchor => Size > 0 ? members[0].transform : transform;

    // Posición del grupo (la del primer cliente)
    public Vector2 Position =>
        Size > 0 ? (Vector2)members[0].transform.position : (Vector2)transform.position;

    // Tipos de cliente del grupo (p. ej. "Pájaro azul"), para el plato especial
    public string[] MemberTypes
    {
        get
        {
            string[] types = new string[Size];
            for (int i = 0; i < Size; i++) types[i] = members[i].TypeName;
            return types;
        }
    }

    private bool draining;
    private float phaseDuration;
    private float patienceLeft;
    private float stateTimer;
    private float moneyOwed;   // lo que han consumido y aún no han pagado
    private bool everAnnoyed;  // si alguna vez bajó del 50% (sin propina)
    private MenuItem[] memberOrders;
    private CustomerQueue queue;
    private Vector2 exitPosition;

    private void Awake()
    {
        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    // Lo llama el CustomerSpawner justo después de crear el grupo
    public void Init(int size, CustomerLook[] looks, CustomerQueue queue, Vector2 exitPosition)
    {
        Size = Mathf.Clamp(size, 1, members.Length);
        this.queue = queue;
        this.exitPosition = exitPosition;
        memberOrders = new MenuItem[members.Length];

        for (int i = 0; i < members.Length; i++)
        {
            bool active = i < Size;
            members[i].gameObject.SetActive(active);
            if (!active) continue;
            if (looks != null && i < looks.Length) members[i].SetLook(looks[i]);
            members[i].SetOrder(null);
        }

        SetSelected(false);
        EnterState(GroupState.InQueue);
    }

    // ================= Acciones que piden otros scripts =================

    public void SetQueuePosition(Vector2 slotPosition)
    {
        for (int i = 0; i < Size; i++)
            members[i].WalkTo(slotPosition + queueMemberOffset * i);
    }

    public void SeatAt(Table table)
    {
        if (State != GroupState.InQueue) return;

        queue.Remove(this);
        AssignedTable = table;
        table.AssignGroup(this);
        EnterState(GroupState.WalkingToTable);

        for (int i = 0; i < Size; i++)
            members[i].WalkTo(table.GetSeatPosition(i));
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        for (int i = 0; i < Size; i++) members[i].SetSelected(selected);
    }

    // Al cerrar el restaurante: los de la cola se van sin subir la ira
    public void SendHome()
    {
        if (State == GroupState.InQueue) Leave();
    }

    // La mesa llama a esto cuando la camarera llega a ella
    public void OnWaitressArrived(WaitressController waitress)
    {
        WaitressHands hands = waitress.Hands;

        // Los cubiertos se pueden poner en cualquier momento mientras estén sentados y antes de comer
        bool canReceiveCutlery = State >= GroupState.Seated && State <= GroupState.WaitingForFood;
        if (!HasCutlery && canReceiveCutlery && hands.RemoveFirst(h => h.type == HeldItemType.Cutlery))
        {
            HasCutlery = true;
            AssignedTable.SetCutlery(true);
            Debug.Log($"{name}: pones la mesa (cubiertos).");
        }

        switch (State)
        {
            case GroupState.WantsToOrderDrinks:
                TakeOrder(Kitchen.Instance.Drinks);
                EnterState(GroupState.WaitingForDrinks);
                break;

            case GroupState.WantsToOrderFood:
                TakeOrder(Kitchen.Instance.Foods);
                EnterState(GroupState.WaitingForFood);
                break;

            case GroupState.WaitingForDrinks:
            case GroupState.WaitingForFood:
                TryDeliver(hands);
                break;

            case GroupState.WantsMenu:
                if (hands.RemoveFirst(h => h.type == HeldItemType.Menu))
                {
                    Debug.Log($"{name}: les das la carta.");
                    EnterState(GroupState.ChoosingFood);
                }
                else GameMessages.Show("Quieren la CARTA: cógela en el stand");
                break;

            case GroupState.WantsBill:
                if (hands.RemoveFirst(h => h.type == HeldItemType.Bill)) Pay();
                else GameMessages.Show("Quieren la CUENTA: sácala en el TPV");
                break;
        }
    }

    // ================= Trastero =================

    // La camarera se los va a llevar: dejan la mesa, se paran y ya no cuentan para nada
    public void BeginBeingTaken()
    {
        if (State == GroupState.InQueue && queue != null) queue.Remove(this);

        if (AssignedTable != null)
        {
            AssignedTable.ReleaseGroup(HasConsumedSomething);
            AssignedTable = null;
        }

        if (moneyOwed > 0f)
            Debug.Log($"{name}: se quedan sin pagar {moneyOwed:0.##} € (ya da igual...).");
        moneyOwed = 0f;

        for (int i = 0; i < Size; i++)
        {
            memberOrders[i] = null;
            members[i].SetOrder(null);
            members[i].IgnoreFreeze = true;
            members[i].StopWalking();
        }

        SetSelected(false);
        SetAlert(AlertType.None);
        draining = false;
        State = GroupState.BeingTaken;
    }

    // Los clientes caminan detrás de un punto (la camarera)
    public void FollowTo(Vector2 basePosition, Vector2 spacing)
    {
        for (int i = 0; i < Size; i++)
            members[i].WalkToDirect(basePosition + spacing * (i + 1));
    }

    public void HideMembers()
    {
        for (int i = 0; i < Size; i++) members[i].gameObject.SetActive(false);
    }

    // ================= Bucle =================

    private void Update()
    {
        if (State == GroupState.BeingTaken) return; // lo controla el StoreroomManager

        if (draining) Drain();

        switch (State)
        {
            case GroupState.WalkingToTable:
                if (AllMembersArrived()) EnterState(GroupState.Seated);
                break;

            case GroupState.Seated:
                if (TickTimer()) EnterState(GroupState.WantsToOrderDrinks);
                break;

            case GroupState.Drinking:
                if (TickTimer())
                {
                    if (AssignedTable != null) AssignedTable.FinishConsuming(ItemCategory.Drink);
                    // Si el restaurante ya ha cerrado, no piden comida: piden la cuenta
                    bool closed = DayManager.Instance != null && DayManager.Instance.IsClosed;
                    bool wantsFood = ForcedFoodChoice ?? (!closed && Random.value < chanceToOrderFood);
                    EnterState(wantsFood ? GroupState.WantsMenu : GroupState.WantsBill);
                }
                break;

            case GroupState.ChoosingFood:
                if (TickTimer()) EnterState(GroupState.WantsToOrderFood);
                break;

            case GroupState.Eating:
                if (TickTimer())
                {
                    if (AssignedTable != null) AssignedTable.FinishConsuming(ItemCategory.Food);
                    EnterState(GroupState.WantsBill);
                }
                break;

            case GroupState.Leaving:
                if (AllMembersArrived()) Destroy(gameObject);
                break;
        }
    }

    // Cambia de fase: aquí se decide qué paciencia tiene, qué aviso enseña, etc.
    private void EnterState(GroupState newState)
    {
        State = newState;
        draining = false;
        SetAlert(AlertType.None);

        if (newState != GroupState.Leaving)
        {
            Mood = CustomerMood.Happy;
            UpdateFaces();
        }

        switch (newState)
        {
            case GroupState.InQueue: StartPatience(queuePatience); break;
            case GroupState.Seated: stateTimer = thinkingTime; break;
            case GroupState.WantsToOrderDrinks: StartPatience(orderDrinksPatience); SetAlert(AlertType.Order); break;
            case GroupState.WaitingForDrinks: StartPatience(waitDrinksPatience); break;
            case GroupState.Drinking: stateTimer = drinkingTime; break;
            case GroupState.WantsMenu: StartPatience(menuPatience); SetAlert(AlertType.Menu); break;
            case GroupState.ChoosingFood: stateTimer = choosingTime; break;
            case GroupState.WantsToOrderFood: StartPatience(orderFoodPatience); SetAlert(AlertType.Order); break;
            case GroupState.WaitingForFood: StartPatience(waitFoodPatience); break;
            case GroupState.Eating: stateTimer = eatingTime; break;
            case GroupState.WantsBill: StartPatience(billPatience); SetAlert(AlertType.Bill); break;
        }
    }

    private void StartPatience(float seconds)
    {
        phaseDuration = Mathf.Max(0.1f, seconds);
        patienceLeft = phaseDuration;
        draining = true;
    }

    private bool TickTimer()
    {
        stateTimer -= GameTime.Delta;
        return stateTimer <= 0f;
    }

    // ================= Pedidos =================

    private void TakeOrder(List<MenuItem> options)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogError("No hay productos de ese tipo en la carta de la Kitchen.");
            return;
        }

        for (int i = 0; i < Size; i++)
        {
            memberOrders[i] = options[Random.Range(0, options.Count)];
            members[i].SetOrder(memberOrders[i]);
        }
        Debug.Log($"{name}: tomas nota.");
    }

    private void TryDeliver(WaitressHands hands)
    {
        bool deliveredSomething = false;
        bool carryingWrongProduct = false;

        for (int h = 0; h < WaitressHands.HandCount; h++)
        {
            HeldItem held = hands.Get(h);
            if (held == null || held.type != HeldItemType.Product) continue;

            int member = FindMemberWaitingFor(held.product);
            if (member < 0)
            {
                carryingWrongProduct = true;
                continue;
            }

            if (held.product.category == ItemCategory.Food && !HasCutlery)
            {
                Debug.Log($"{name}: ¡no tienen la mesa puesta! Trae cubiertos antes de servir la comida.");
                GameMessages.Show("Sin cubiertos no puedes servir la comida");
                continue;
            }

            hands.RemoveAt(h);
            memberOrders[member] = null;
            members[member].SetOrder(null);
            if (AssignedTable != null) AssignedTable.ShowServed(member, held.product); // se queda en la mesa
            HasConsumedSomething = true;
            if (MoneyManager.Instance != null) moneyOwed += MoneyManager.Instance.PriceOf(held.product);
            deliveredSomething = true;
            Debug.Log($"{name}: sirves {held.product.displayName}.");
        }

        if (!deliveredSomething && carryingWrongProduct) WrongItem();

        if (AllOrdersDelivered())
            EnterState(State == GroupState.WaitingForDrinks ? GroupState.Drinking : GroupState.Eating);
    }

    private int FindMemberWaitingFor(MenuItem product)
    {
        for (int i = 0; i < Size; i++)
            if (memberOrders[i] != null && product.Matches(memberOrders[i])) return i;
        return -1;
    }

    private bool AllOrdersDelivered()
    {
        for (int i = 0; i < Size; i++)
            if (memberOrders[i] != null) return false;
        return true;
    }

    private void WrongItem()
    {
        Debug.Log($"{name}: ¡esto no es lo que hemos pedido!");
        GameMessages.Show("¡Esto no es lo que hemos pedido!");
        patienceLeft -= phaseDuration * wrongItemPatienceLoss;
        AddRage(wrongItemRage);
    }

    private void Pay()
    {
        float tip = everAnnoyed || MoneyManager.Instance == null ? 0f : MoneyManager.Instance.TipAmount;
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.Add(moneyOwed, $"{name} paga");
            GameMessages.Show(tip > 0f ? $"+{moneyOwed:0} € y {tip:0} € de propina" : $"+{moneyOwed:0} €");
            if (tip > 0f) MoneyManager.Instance.Add(tip, $"propina de {name}");
        }
        moneyOwed = 0f;
        Leave();
    }

    // ================= Paciencia e ira =================

    private void Drain()
    {
        if (PatiencePaused) return;
        patienceLeft -= GameTime.Delta;
        UpdateMood();

        float ragePerSecond = Mood switch
        {
            CustomerMood.Annoyed => ragePerSecondAnnoyed,
            CustomerMood.Angry => ragePerSecondAngry,
            _ => 0f
        };
        AddRage(ragePerSecond * GameTime.Delta);

        if (patienceLeft <= 0f) LeaveUnhappy();
    }

    private void UpdateMood()
    {
        float ratio = PatienceNormalized;
        CustomerMood newMood =
            ratio < angryThreshold ? CustomerMood.Angry :
            ratio < annoyedThreshold ? CustomerMood.Annoyed :
            CustomerMood.Happy;

        if (newMood == Mood) return;

        if (Mood < CustomerMood.Annoyed && newMood >= CustomerMood.Annoyed) AddRage(rageOnAnnoyed);
        if (Mood < CustomerMood.Angry && newMood >= CustomerMood.Angry) AddRage(rageOnAngry);
        if (newMood >= CustomerMood.Annoyed) everAnnoyed = true;

        Mood = newMood;
        UpdateFaces();
    }

    private void LeaveUnhappy()
    {
        if (State == GroupState.InQueue)
        {
            Debug.Log($"{name}: se cansan de esperar en la cola y se van.");
            GameMessages.Show("Un grupo se ha cansado de esperar en la cola");
            if (RageMeter.Instance != null) RageMeter.Instance.AddRage(rageOnLeaveQueue, this); // sin reducir
        }
        else
        {
            float lost = moneyOwed;
            Debug.Log($"{name}: ¡se van sin pagar! Pierdes {lost:0.##} €.");
            GameMessages.Show($"¡Se van sin pagar! -{lost:0} €");
            if (lost > 0f && MoneyManager.Instance != null)
                MoneyManager.Instance.Add(-lost, $"{name} se va sin pagar");
            AddRage(rageOnLeaveTable + lost * ragePerMoneyLost);
            moneyOwed = 0f;
        }
        Leave();
    }

    private void Leave()
    {
        if (State == GroupState.Leaving || State == GroupState.BeingTaken) return;

        if (State == GroupState.InQueue && queue != null) queue.Remove(this);

        if (AssignedTable != null)
        {
            AssignedTable.ReleaseGroup(HasConsumedSomething);
            AssignedTable = null;
        }

        for (int i = 0; i < Size; i++)
        {
            memberOrders[i] = null;
            members[i].SetOrder(null);
        }

        SetSelected(false);
        EnterState(GroupState.Leaving);

        for (int i = 0; i < Size; i++) members[i].WalkTo(exitPosition);
    }

    // ================= Visual y utilidades =================

    private void UpdateFaces()
    {
        Color c = Mood switch
        {
            CustomerMood.Annoyed => annoyedColor,
            CustomerMood.Angry => angryColor,
            _ => happyColor
        };
        for (int i = 0; i < Size; i++) members[i].SetFaceColor(c, Mood == CustomerMood.Happy);
    }

    private void SetAlert(AlertType type)
    {
        if (Size == 0) return;
        switch (type)
        {
            case AlertType.Order: members[0].SetAlert(true, orderAlertIcon, orderAlertColor, "¡PEDIR!"); break;
            case AlertType.Menu: members[0].SetAlert(true, menuAlertIcon, menuAlertColor, "CARTA"); break;
            case AlertType.Bill: members[0].SetAlert(true, billAlertIcon, billAlertColor, "CUENTA"); break;
            default: members[0].SetAlert(false, null, Color.white); break;
        }
    }

    public bool AllMembersArrived()
    {
        for (int i = 0; i < Size; i++)
            if (members[i].IsWalking) return false;
        return true;
    }

    private void AddRage(float amount)
    {
        if (State == GroupState.InQueue) amount *= queueRageMultiplier;
        if (RageMeter.Instance != null) RageMeter.Instance.AddRage(amount, this);
    }
}
