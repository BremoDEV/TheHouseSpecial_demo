using System;
using System.Collections.Generic;
using UnityEngine;

// Tono del comentario. De momento solo sirve para clasificar las frases
// (no cambia la paciencia ni la ira).
public enum CustomerCommentType
{
    Neutral,
    Positive,
    Annoying,
    Harassing
}

[Serializable]
public class CustomerCommentEntry
{
    [TextArea(1, 3)]
    public string text;

    public CustomerCommentType type = CustomerCommentType.Neutral;

    public CustomerCommentEntry(string text, CustomerCommentType type)
    {
        this.text = text;
        this.type = type;
    }
}

// Comentarios aleatorios de los clientes sentados.
// Cada pocos segundos elige un grupo sentado al azar y uno de sus miembros suelta una frase
// de la lista durante 'visibleDuration' segundos. Solo hay un comentario a la vez.
// Se para con el tiempo del juego (GameTime) y si el grupo se levanta, se quita el comentario.
// Va en un objeto de cada escena de día (lo añade Tools → The House Special → Añadir comentarios de clientes).
public class CustomerCommentManager : MonoBehaviour
{
    [Tooltip("Frases que pueden decir los clientes")]
    [SerializeField] private CustomerCommentEntry[] comments =
    {
        new CustomerCommentEntry("¡Qué hambre tengo!", CustomerCommentType.Positive),
        new CustomerCommentEntry("Espero que la comida esté buena.", CustomerCommentType.Neutral),
        new CustomerCommentEntry("¿Cuánto falta?", CustomerCommentType.Annoying),
        new CustomerCommentEntry("¿Nos atienden pronto?", CustomerCommentType.Annoying),
        new CustomerCommentEntry("Este sitio tiene personalidad.", CustomerCommentType.Neutral),
        new CustomerCommentEntry("¡Todo huele increíble!", CustomerCommentType.Positive),
        new CustomerCommentEntry("¿Puedes venir un segundo?", CustomerCommentType.Neutral),
        new CustomerCommentEntry("Bueno, seguimos esperando...", CustomerCommentType.Annoying),
        new CustomerCommentEntry("¿Esto viene así o es una sorpresa?", CustomerCommentType.Annoying),
        new CustomerCommentEntry("Menos mal que no cocino yo.", CustomerCommentType.Neutral),
        new CustomerCommentEntry("¿Siempre hay tanto ruido?", CustomerCommentType.Annoying),
        new CustomerCommentEntry("¿Podemos pedir ya?", CustomerCommentType.Annoying),
        new CustomerCommentEntry("La carta parece un acertijo.", CustomerCommentType.Neutral),
        new CustomerCommentEntry("¿Me estás ignorando o es parte del servicio?", CustomerCommentType.Harassing)
    };

    [Tooltip("Segundos mínimos entre un comentario y el siguiente")]
    [SerializeField] private float minInterval = 5f;
    [Tooltip("Segundos máximos entre un comentario y el siguiente")]
    [SerializeField] private float maxInterval = 10f;
    [Tooltip("Segundos que se ve cada comentario")]
    [SerializeField] private float visibleDuration = 3f;

    private float timer;
    private CustomerGroup activeGroup;
    private Customer activeCustomer;
    private bool showingComment;

    private readonly List<CustomerGroup> eligibleGroups =
        new List<CustomerGroup>();

    private void Start()
    {
        ScheduleNextComment();
    }

    private void Update()
    {
        float delta = GameTime.Delta;

        if (delta <= 0f)
            return;

        if (showingComment)
        {
            if (!IsEligible(activeGroup) ||
                activeCustomer == null ||
                !activeCustomer.gameObject.activeInHierarchy)
            {
                HideActiveComment();
                ScheduleNextComment();
                return;
            }

            timer -= delta;

            if (timer <= 0f)
            {
                HideActiveComment();
                ScheduleNextComment();
            }

            return;
        }

        timer -= delta;

        if (timer <= 0f && !TryShowRandomComment())
            timer = 1f;
    }

    // Busca un grupo sentado al azar y un miembro suyo sin comentario. Devuelve false si no hay nadie.
    private bool TryShowRandomComment()
    {
        if (comments == null || comments.Length == 0)
            return false;

        eligibleGroups.Clear();

        for (int i = 0; i < CustomerGroup.All.Count; i++)
        {
            CustomerGroup group = CustomerGroup.All[i];

            if (IsEligible(group))
                eligibleGroups.Add(group);
        }

        if (eligibleGroups.Count == 0)
            return false;

        while (eligibleGroups.Count > 0)
        {
            int groupIndex = UnityEngine.Random.Range(0, eligibleGroups.Count);
            CustomerGroup group = eligibleGroups[groupIndex];
            eligibleGroups.RemoveAt(groupIndex);

            int firstMember = UnityEngine.Random.Range(0, group.Size);

            for (int offset = 0; offset < group.Size; offset++)
            {
                int memberIndex = (firstMember + offset) % group.Size;
                Customer customer = group.GetMember(memberIndex);

                if (customer == null || customer.IsCommentVisible)
                    continue;

                CustomerCommentEntry entry = GetRandomComment();

                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;

                if (!customer.ShowComment(entry.text))
                    continue;

                activeGroup = group;
                activeCustomer = customer;
                showingComment = true;
                timer = Mathf.Max(0.1f, visibleDuration);
                return true;
            }
        }

        return false;
    }

    private CustomerCommentEntry GetRandomComment()
    {
        if (comments == null || comments.Length == 0)
            return null;

        for (int attempt = 0; attempt < comments.Length; attempt++)
        {
            CustomerCommentEntry entry =
                comments[UnityEngine.Random.Range(0, comments.Length)];

            if (entry != null && !string.IsNullOrWhiteSpace(entry.text))
                return entry;
        }

        return null;
    }

    // Solo comentan los grupos que están en su mesa (desde que se sientan hasta que piden la cuenta)
    private bool IsEligible(CustomerGroup group)
    {
        if (group == null || !group.gameObject.activeInHierarchy)
            return false;

        if (group.AssignedTable == null)
            return false;

        return (int)group.State >= (int)GroupState.Seated &&
               (int)group.State <= (int)GroupState.WantsBill;
    }

    private void HideActiveComment()
    {
        if (activeCustomer != null)
            activeCustomer.HideComment();

        activeCustomer = null;
        activeGroup = null;
        showingComment = false;
    }

    private void ScheduleNextComment()
    {
        float min = Mathf.Max(0.1f, Mathf.Min(minInterval, maxInterval));
        float max = Mathf.Max(min, maxInterval);
        timer = UnityEngine.Random.Range(min, max);
    }

    private void OnDisable()
    {
        HideActiveComment();
    }
}
