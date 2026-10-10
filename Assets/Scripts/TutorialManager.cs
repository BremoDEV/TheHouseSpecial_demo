using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// El tutorial: guía al jugador paso a paso con una sola mesa.
// Cada paso enseña un texto y, o bien espera a que el jugador HAGA algo (Step),
// o bien espera a que pulse "Continuar" (Info). Una flecha señala dónde hay que clicar.
//
// Reglas especiales del tutorial (se quitan al salir de la escena):
// - La paciencia de los clientes no baja y nada se pone malo en la barra.
// - La ira solo cambia cuando el tutorial quiere.
// - No hay reloj ni fin de día (el DayManager está desactivado en esta escena).
//
// Para cambiar los textos o el orden: todo está en la corrutina RunTutorial, de arriba abajo.
public class TutorialManager : MonoBehaviour
{
    [Header("Escena a la que se va al terminar o al saltar el tutorial")]
    [SerializeField] private string gameSceneName = "Demo_Restaurante";

    [Header("Interfaz del tutorial")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Botón 'Continuar' (solo se ve en los pasos de explicación)")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("Flecha que señala dónde clicar")]
    [SerializeField] private Transform arrow;
    [SerializeField] private float arrowHeight = 0.45f;
    [SerializeField] private float arrowBobAmount = 0.15f;
    [SerializeField] private float arrowBobSpeed = 4f;

    [Header("Objetos de la escena que se señalan")]
    [SerializeField] private CustomerSpawner spawner;
    [SerializeField] private Table table;
    [SerializeField] private Transform tpv;
    [SerializeField] private Transform menus;      // stand de cartas
    [SerializeField] private Transform cutlery;    // cubiertos de la barra
    [SerializeField] private Transform dishReturn; // sucios y sobras
    [SerializeField] private Transform storeroom;
    [SerializeField] private WaitressController waitress;
    [SerializeField] private TpvPanel tpvPanel;

    [Header("Clientes del tutorial (nombres de los Looks del CustomerSpawner)")]
    [SerializeField] private string firstCustomerType = "Gato";
    [SerializeField] private string specialCustomerType = "Pájaro azul";

    private Transform arrowTarget;
    private bool continuePressed;

    // ================================================================= ARRANQUE

    private void Awake()
    {
        Time.timeScale = 1f;
        GameTime.Frozen = false;
        CounterItem.SpoilPaused = true;
    }

    private void Start()
    {
        if (continueButton != null) continueButton.onClick.AddListener(() => continuePressed = true);
        if (skipButton != null) skipButton.onClick.AddListener(GoToGame);
        if (spawner != null) spawner.spawningEnabled = false; // los clientes los trae el tutorial
        if (RageMeter.Instance != null) RageMeter.Instance.IgnoreRage = true;

        StartCoroutine(RunTutorial());
    }

    private void OnDestroy()
    {
        // Al salir de la escena, todo vuelve a funcionar normal
        CounterItem.SpoilPaused = false;
    }

    // ================================================================= EL GUION

    private IEnumerator RunTutorial()
    {
        // --- Bienvenida ---
        yield return Info("¡Bienvenida a The House Special! Eres la camarera.\n" +
                          "Vamos a practicar con una sola mesa. Tranquila: aquí nadie pierde la paciencia.");

        yield return Info("Para moverte, haz clic donde quieras ir. " +
                          "Si haces clic en algo (una mesa, la barra, el TPV...), la camarera irá y lo usará.");

        // --- Sentar al cliente ---
        CustomerGroup group = spawner.SpawnNow(1, firstCustomerType);
        group.PatiencePaused = true;
        group.ForcedFoodChoice = true; // en el tutorial siempre piden comida

        yield return Step("Ha llegado un cliente a la COLA. Haz clic en él para seleccionarlo.",
            () => group.IsSelected || group.State != GroupState.InQueue, group.Anchor);

        yield return Step("Ahora haz clic en la MESA para sentarlo.\nSolo se puede sentar a gente en mesas limpias y vacías.",
            () => group.State != GroupState.InQueue, table.transform);

        // --- Bebida ---
        yield return Step("Está mirando qué tomar... Cuando salga ¡PEDIR!, haz clic en la mesa (o en el bocadillo) para tomar nota.",
            () => group.State >= GroupState.WaitingForDrinks, table.transform);

        yield return Info("Encima del cliente ves lo que ha pedido.\n" +
                          "Su CARA es su paciencia: verde, amarilla, roja... y si se acaba, se va sin pagar. Aquí no baja.");

        yield return Step("Ve al TPV y haz clic en él para hacer el pedido a cocina.",
            () => tpvPanel.IsOpen || OrderIsOnTheWay(group, GroupState.Drinking), tpv);

        yield return Step("Pulsa la bebida que ha pedido para apuntarla en la comanda y después el botón verde PEDIR.",
            () => OrderIsOnTheWay(group, GroupState.Drinking), null);

        yield return PickUpFromCounter("La cocina lo prepara. Cuando aparezca en la BARRA, haz clic en él para cogerlo.",
            group, GroupState.Drinking);

        yield return Step("Llévaselo: haz clic en la mesa.",
            () => group.State >= GroupState.Drinking, table.transform);

        // --- Carta ---
        yield return Step("Está bebiendo. Después pedirá la CARTA (para comer) o la CUENTA (para irse).",
            () => group.State >= GroupState.WantsMenu, table.transform);

        yield return Step("Quiere la CARTA. Coge una en el stand de CARTAS y llévasela a la mesa.",
            () => group.State >= GroupState.ChoosingFood, menus,
            () => HoldsItem(HeldItemType.Menu) ? table.transform : menus);

        // --- Cubiertos ---
        yield return Step("Para comer necesita CUBIERTOS. Cógelos en la punta de la barra y llévaselos.\n" +
                          "Sin cubiertos no se puede servir la comida.",
            () => group.HasCutlery, cutlery, () => HoldsItem(HeldItemType.Cutlery) ? table.transform : cutlery);

        // --- Comida ---
        yield return Step("Cuando salga ¡PEDIR!, toma nota de la comida.",
            () => group.State >= GroupState.WaitingForFood, table.transform);

        yield return Step("Pide la comida en el TPV, igual que la bebida.",
            () => OrderIsOnTheWay(group, GroupState.Eating), tpv, () => tpvPanel.IsOpen ? null : tpv);

        yield return PickUpFromCounter("Recógela en la BARRA cuando esté lista.", group, GroupState.Eating);

        yield return Step("Sírvesela: haz clic en la mesa.",
            () => group.State >= GroupState.Eating, table.transform);

        // --- Cuenta ---
        yield return Step("Está comiendo. Al terminar pedirá la CUENTA.",
            () => group.State >= GroupState.WantsBill, table.transform);

        yield return Step("Quiere la CUENTA (el aviso azul). Ve al TPV, pulsa el botón azul CUENTA y llévasela.",
            () => group == null || group.State == GroupState.Leaving, tpv,
            () => HoldsItem(HeldItemType.Bill) ? table.transform : (tpvPanel.IsOpen ? null : tpv));

        yield return Info("¡Ha pagado! El dinero sube arriba. Tu objetivo del día es llegar al mínimo antes de las 20:00.\n" +
                          "Si el cliente nunca se enfada, además deja propina.");

        // --- Recoger ---
        yield return Step("La mesa se ha quedado SUCIA. Haz clic en ella para recoger los platos.",
            () => table.State != TableState.Dirty, table.transform);

        yield return Step("Deja los platos en SUCIOS Y SOBRAS. Ahí también puedes tirar cualquier cosa que te sobre.",
            () => !HoldsItem(HeldItemType.DirtyDishes), dishReturn);

        // --- Ira ---
        RageMeter.Instance.SetRage(60f);
        yield return Info("Arriba a la izquierda está tu IRA. Sube cuando los clientes se enfadan, esperan demasiado o se van sin pagar.\n" +
                          "Con más ira vas más rápido... pero te cuesta leer los pedidos.");

        yield return Step("Para calmarte, entra al TRASTERO a descansar. Pero ojo: el restaurante sigue funcionando sin ti.",
            () => StoreroomManager.Instance.IsResting || RageMeter.Instance.Current <= 0f, storeroom);

        yield return Step("Mientras descansas, la ira baja. Sales sola al llegar a 0, o al hacer clic en cualquier sitio.",
            () => !StoreroomManager.Instance.IsResting, null);

        // --- Trastero ---
        CustomerGroup special = spawner.SpawnNow(1, specialCustomerType);
        special.PatiencePaused = true;
        yield return WaitSeconds(2f, special.Anchor);

        yield return Info("Ha llegado otro cliente... Y ahora imagina que, después de un día horrible, te saca de quicio.");

        // Llenamos la ira a propósito: la camarera se lleva al cliente al trastero ella sola
        RageMeter.Instance.IgnoreRage = false;
        RageMeter.Instance.AddRage(10000f, special);
        RageMeter.Instance.IgnoreRage = true;
        SetText("Cuando la IRA se llena, pierdes el control...");
        SetContinueVisible(false);
        arrowTarget = null;
        yield return null;
        while (special != null || StoreroomManager.Instance.IsKilling) yield return null;

        yield return Info("...y la camarera se encarga del cliente en el trastero. La ira vuelve a 0.\n" +
                          "Era un PÁJARO AZUL, así que en la carta aparece un plato nuevo: Pollo con plumas azules. ¡Vale más!");

        // --- Final ---
        yield return Info("¡Ya sabes todo lo necesario! Atiende rápido, no dejes que se enfaden y llega al mínimo de dinero.\n" +
                          "Pulsa Continuar para empezar el día.");

        GoToGame();
    }

    // ================================================================= PIEZAS DEL GUION

    // Paso de explicación: enseña el texto y espera a que el jugador pulse "Continuar"
    private IEnumerator Info(string text, Transform target = null)
    {
        SetText(text);
        arrowTarget = target;
        SetContinueVisible(true);
        continuePressed = false;
        while (!continuePressed) yield return null;
        SetContinueVisible(false);
    }

    // Paso de acción: enseña el texto y espera hasta que se cumpla 'done'.
    // 'targetUpdate' (opcional) decide en cada frame a qué apunta la flecha.
    private IEnumerator Step(string text, Func<bool> done, Transform target, Func<Transform> targetUpdate = null)
    {
        SetText(text);
        SetContinueVisible(false);
        arrowTarget = target;
        while (!done())
        {
            if (targetUpdate != null) arrowTarget = targetUpdate();
            yield return null;
        }
    }

    // Espera a tener en la mano lo que ha pedido el cliente (la flecha señala el producto en la barra).
    // servedState: el estado del cliente cuando ya está servido (Drinking o Eating), por si el jugador va más rápido.
    private IEnumerator PickUpFromCounter(string text, CustomerGroup group, GroupState servedState)
    {
        SetText(text);
        SetContinueVisible(false);
        while (!HoldsItem(HeldItemType.Product) && !ServedAlready(group, servedState))
        {
            CounterItem item = FindAnyObjectByType<CounterItem>();
            arrowTarget = item != null ? item.transform : null;
            yield return null;
        }
    }

    private IEnumerator WaitSeconds(float seconds, Transform target)
    {
        arrowTarget = target;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    // ================================================================= COMPROBACIONES

    private bool HoldsItem(HeldItemType type)
    {
        for (int h = 0; h < WaitressHands.HandCount; h++)
        {
            HeldItem held = waitress.Hands.Get(h);
            if (held != null && held.type == type) return true;
        }
        return false;
    }

    // ¿Ya está pedido? (en cocina, en la barra, en la mano o servido)
    private bool OrderIsOnTheWay(CustomerGroup group, GroupState servedState)
    {
        if (Kitchen.Instance != null && Kitchen.Instance.UsedCapacity > 0) return true;
        return HoldsItem(HeldItemType.Product) || ServedAlready(group, servedState);
    }

    private static bool ServedAlready(CustomerGroup group, GroupState servedState)
    {
        return group == null || group.State >= servedState;
    }

    // ================================================================= INTERFAZ

    private void SetText(string text)
    {
        if (panel != null) panel.SetActive(true);
        if (messageText != null) messageText.text = text;
    }

    private void SetContinueVisible(bool visible)
    {
        if (continueButton != null) continueButton.gameObject.SetActive(visible);
    }

    private void Update()
    {
        UpdateArrow();
    }

    // Coloca la flecha encima de lo que hay que clicar, subiendo y bajando
    private void UpdateArrow()
    {
        if (arrow == null) return;

        bool show = arrowTarget != null && arrowTarget.gameObject.activeInHierarchy;
        if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        if (!show) return;

        // Busca la parte más alta de lo que se ve del objetivo (para no taparlo)
        float top = arrowTarget.position.y;
        float centerX = arrowTarget.position.x;
        bool found = false;
        foreach (Renderer r in arrowTarget.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!found) { centerX = r.bounds.center.x; found = true; }
            top = Mathf.Max(top, r.bounds.max.y);
        }

        float bob = Mathf.Sin(Time.unscaledTime * arrowBobSpeed) * arrowBobAmount;
        arrow.position = new Vector3(centerX, top + arrowHeight + bob, 0f);
    }

    // ================================================================= SALIR

    private void GoToGame()
    {
        Time.timeScale = 1f;
        GameTime.Frozen = false;
        SceneManager.LoadScene(gameSceneName);
    }
}
