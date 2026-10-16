using System;
using System.Collections;
using UnityEngine;

// Controla el trastero:
// 1) LLEVARSE AL CLIENTE: cuando la ira se llena, el jugador pierde el control,
//    el tiempo del restaurante se congela, la camarera va a por el grupo que la llenó,
//    se lo lleva al trastero, pantalla en negro... y vuelve con la ira vacía.
// 2) DESCANSAR: si la camarera entra al trastero por su cuenta, la ira baja poco a poco
//    pero el restaurante sigue funcionando. Cualquier clic la saca.
public class StoreroomManager : MonoBehaviour
{
    public static StoreroomManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private WaitressController waitress;
    [Tooltip("La puerta del trastero (el objeto con el script Storeroom)")]
    [SerializeField] private Storeroom storeroom;
    [Tooltip("Panel negro a pantalla completa (con la silueta si la hay). Se desactiva solo al empezar.")]
    [SerializeField] private GameObject blackoutPanel;

    [Header("Llevarse al cliente (editable por diseño)")]
    [Tooltip("Segundos que dura la pantalla en negro")]
    [SerializeField] private float blackoutDuration = 2.5f;
    [Tooltip("Ira con la que te quedas después de 'encargarte' del cliente")]
    [SerializeField] private float rageAfterKill = 0f;
    [Tooltip("Separación de los clientes detrás de la camarera mientras los lleva")]
    [SerializeField] private Vector2 followSpacing = new Vector2(-0.6f, 0f);
    [Tooltip("Por seguridad: segundos máximos de cada paseo (por si alguien se atasca)")]
    [SerializeField] private float maxWalkTime = 10f;

    [Header("Descanso (editable por diseño)")]
    [Tooltip("Ira que baja por segundo mientras descansas")]
    [SerializeField] private float restRagePerSecond = 8f;
    [Tooltip("Si está activo, sale sola del trastero cuando la ira llega a 0 (si entró sin ira, se queda hasta que hagas clic)")]
    [SerializeField] private bool autoExitWhenCalm = true;

    private bool restHadRage; // si al entrar (o durante el descanso) tenía ira que bajar

    public bool IsKilling { get; private set; }
    public bool IsResting { get; private set; }
    public bool InputLocked => IsKilling;

    private bool rageFullPending;

    private void Awake()
    {
        Instance = this;
        GameTime.Frozen = false;
        if (blackoutPanel != null) blackoutPanel.SetActive(false);
    }

    private void Start()
    {
        if (RageMeter.Instance != null) RageMeter.Instance.onRageFull.AddListener(OnRageFull);
        else Debug.LogError("StoreroomManager: no hay RageMeter en la escena.");
    }

    // No empezamos la secuencia aquí mismo: lo dejamos para el Update,
    // así el script que subió la ira termina lo que estaba haciendo.
    private void OnRageFull()
    {
        rageFullPending = true;
    }

    private void Update()
    {
        if (rageFullPending && !IsKilling)
        {
            rageFullPending = false;
            StartCoroutine(KillSequence(ChooseVictim()));
        }

        if (IsResting && !IsKilling && RageMeter.Instance != null)
        {
            if (RageMeter.Instance.Current > 0f) restHadRage = true;
            RageMeter.Instance.ReduceRage(restRagePerSecond * Time.deltaTime);
            // Antes salía al momento si entraba con la ira a 0 (parecía que no te dejaba entrar)
            if (autoExitWhenCalm && restHadRage && RageMeter.Instance.Current <= 0f) StopRest();
        }
    }

    // ================= Descanso =================

    public void StartRest()
    {
        if (IsKilling || IsResting) return;
        IsResting = true;
        restHadRage = false;
        waitress.SetVisible(false);
        Debug.Log("La camarera entra al trastero a respirar...");
    }

    public void StopRest()
    {
        if (!IsResting) return;
        IsResting = false;
        waitress.SetVisible(true);
        Debug.Log("La camarera sale del trastero.");
    }

    // ================= Llevarse al cliente =================

    private CustomerGroup ChooseVictim()
    {
        // El que llenó la barra
        CustomerGroup source = RageMeter.Instance != null ? RageMeter.Instance.LastRageSource : null;
        if (source != null && source.CanBeTaken) return source;

        // Si ya no existe (se fue del todo), cualquiera sentado; si no, cualquiera
        CustomerGroup fallback = null;
        foreach (CustomerGroup g in CustomerGroup.All)
        {
            if (g == null || !g.CanBeTaken) continue;
            if (g.AssignedTable != null) return g;
            if (fallback == null) fallback = g;
        }
        return fallback;
    }

    private IEnumerator KillSequence(CustomerGroup victim)
    {
        if (victim == null)
        {
            Debug.Log("La ira se ha llenado, pero no queda nadie a quien llevarse.");
            RageMeter.Instance.SetRage(rageAfterKill);
            yield break;
        }

        Debug.Log($"¡La camarera pierde los nervios con {victim.name}!");
        GameMessages.Show("¡La camarera ha perdido los nervios!", 3f);
        IsKilling = true;
        if (IsResting) StopRest();
        GameTime.Frozen = true;
        victim.BeginBeingTaken();

        // 1. Va hacia los clientes
        waitress.MoveTo(victim.Position);
        yield return WaitUntilOrTimeout(() => !waitress.IsMoving);

        // 2. Se los lleva al trastero; ellos la siguen
        waitress.MoveTo(storeroom.InteractionPosition);
        float t = 0f;
        while ((waitress.IsMoving || !victim.AllMembersArrived()) && t < maxWalkTime)
        {
            victim.FollowTo(waitress.transform.position, followSpacing);
            t += Time.deltaTime;
            yield return null;
        }

        // 3. Pantalla en negro
        victim.HideMembers();
        waitress.SetVisible(false);
        if (blackoutPanel != null) blackoutPanel.SetActive(true);
        yield return new WaitForSeconds(blackoutDuration);

        // 4. Resultado: plato especial, contador del día y fuera
        if (Kitchen.Instance != null) Kitchen.Instance.OnCustomerKilled(victim.MemberTypes);
        if (DayManager.Instance != null) DayManager.Instance.RegisterKill();
        Destroy(victim.gameObject);

        RageMeter.Instance.SetRage(rageAfterKill);
        if (blackoutPanel != null) blackoutPanel.SetActive(false);
        waitress.SetVisible(true);
        GameTime.Frozen = false;
        IsKilling = false;
    }

    private IEnumerator WaitUntilOrTimeout(Func<bool> condition)
    {
        float t = 0f;
        while (!condition() && t < maxWalkTime)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
}
