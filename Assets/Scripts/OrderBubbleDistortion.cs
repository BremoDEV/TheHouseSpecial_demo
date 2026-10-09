using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Distorsiona el bocadillo de pedido según la ira de la camarera.
// Va en el objeto "Order" de cada cliente (el bocadillo), y se encarga de su icono.
//
// Tres fases que se suman poco a poco:
//   1) Desde el 50%: el bocadillo se tambalea y tiembla (cada vez más).
//   2) Desde el 75%: el color del icono se desvía (cuesta distinguir productos parecidos).
//   3) Desde el 90%: de vez en cuando, durante un instante, el icono muestra OTRO producto.
//      El pedido real no cambia: es la camarera la que "ve mal". Si te fijas, lo ves bien.
public class OrderBubbleDistortion : MonoBehaviour
{
    [Tooltip("El icono del producto dentro del bocadillo (OrderIcon)")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [Tooltip("Texto opcional con el nombre del producto (también se distorsiona)")]
    [SerializeField] private TMP_Text label;

    [Header("1) Tambaleo (editable por diseño)")]
    [Range(0f, 1f)] [SerializeField] private float wobbleFrom = 0.5f;
    [Tooltip("Grados máximos de giro con la ira al 100%")]
    [SerializeField] private float maxWobbleDegrees = 18f;
    [SerializeField] private float wobbleSpeed = 6f;
    [Tooltip("Temblor máximo en unidades con la ira al 100%")]
    [SerializeField] private float maxShake = 0.06f;

    [Header("2) Color desviado (editable por diseño)")]
    [Range(0f, 1f)] [SerializeField] private float colorShiftFrom = 0.75f;
    [Tooltip("Cuánto se desvía el tono con la ira al 100% (0.15 = bastante)")]
    [Range(0f, 0.5f)] [SerializeField] private float maxHueShift = 0.15f;

    [Header("3) Ver otro producto (editable por diseño)")]
    [Range(0f, 1f)] [SerializeField] private float swapFrom = 0.9f;
    [Tooltip("Veces por segundo (de media) que se ve un producto equivocado, con la ira al 100%")]
    [SerializeField] private float maxSwapsPerSecond = 0.6f;
    [Tooltip("Segundos que dura cada 'visión' equivocada")]
    [SerializeField] private float swapDuration = 0.35f;

    private MenuItem realItem;
    private MenuItem decoyItem;
    private float decoyTimeLeft;
    private float seed;

    private bool initialized;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        seed = Random.value * 100f; // cada bocadillo se mueve distinto
    }

    // Lo llama Customer.SetOrder. null = sin pedido.
    public void SetItem(MenuItem item)
    {
        Initialize();
        realItem = item;
        decoyItem = null;
        decoyTimeLeft = 0f;
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
        ShowItem(item, 0f);
    }

    private void Update()
    {
        if (realItem == null) return;

        float rage = RageMeter.Instance != null ? RageMeter.Instance.Normalized : 0f;

        // 1) Tambaleo y temblor
        float wobble = Intensity(rage, wobbleFrom);
        float angle = Mathf.Sin(Time.time * wobbleSpeed + seed) * maxWobbleDegrees * wobble;
        Vector3 shake = new Vector3(
            Mathf.PerlinNoise(seed, Time.time * 8f) - 0.5f,
            Mathf.PerlinNoise(Time.time * 8f, seed) - 0.5f,
            0f) * 2f * maxShake * wobble;
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, angle);
        transform.localPosition = baseLocalPosition + shake;

        // 3) De vez en cuando, ver otro producto
        if (decoyTimeLeft > 0f)
        {
            decoyTimeLeft -= Time.deltaTime;
            if (decoyTimeLeft <= 0f) decoyItem = null;
        }
        else
        {
            float swapIntensity = Intensity(rage, swapFrom);
            if (swapIntensity > 0f && Random.value < maxSwapsPerSecond * swapIntensity * Time.deltaTime)
            {
                decoyItem = PickDecoy();
                if (decoyItem != null) decoyTimeLeft = swapDuration;
            }
        }

        // 2) Color desviado
        float hueShift = Mathf.Sin(Time.time * 2f + seed) * maxHueShift * Intensity(rage, colorShiftFrom);

        ShowItem(decoyItem != null ? decoyItem : realItem, hueShift);
    }

    // 0 por debajo del umbral, sube hasta 1 cuando la ira llega al 100%
    private static float Intensity(float rage, float from)
    {
        if (rage < from) return 0f;
        return from >= 1f ? 1f : Mathf.InverseLerp(from, 1f, rage);
    }

    private MenuItem PickDecoy()
    {
        if (Kitchen.Instance == null) return null;
        List<MenuItem> pool = realItem.category == ItemCategory.Drink ? Kitchen.Instance.Drinks : Kitchen.Instance.Foods;

        List<MenuItem> options = new List<MenuItem>();
        foreach (MenuItem m in pool)
            if (m != null && !m.Matches(realItem)) options.Add(m);

        return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }

    private void ShowItem(MenuItem item, float hueShift)
    {
        if (item == null) return;
        if (label != null) label.text = item.displayName;
        if (iconRenderer == null) return;
        if (item.icon != null) iconRenderer.sprite = item.icon;

        Color c = item.placeholderColor;
        if (Mathf.Abs(hueShift) > 0.0001f)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + hueShift, 1f);
            c = Color.HSVToRGB(h, s, v);
            c.a = item.placeholderColor.a;
        }
        iconRenderer.color = c;
    }
}
