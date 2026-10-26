using UnityEngine;

// Enseña en el restaurante el dibujo de una mejora comprada (p. ej. la tele en la pared).
// Sin la mejora, el objeto se oculta. Con nivel 1 o 2 usa el dibujo de ese nivel del catálogo de mejoras.
[RequireComponent(typeof(SpriteRenderer))]
public class UpgradeVisual : MonoBehaviour
{
    [SerializeField] private UpgradeId upgrade = UpgradeId.Television;
    [Tooltip("Ancho del dibujo en unidades")]
    [SerializeField] private float width = 1.1f;

    private void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        int level = RunState.Level(upgrade);
        UpgradeDef def = UpgradeCatalog.Instance != null ? UpgradeCatalog.Instance.Get(upgrade) : null;
        Sprite sprite = def != null ? def.IconFor(level) : null;
        bool show = level > 0 && sprite != null;
        sr.enabled = show;
        if (!show) return;
        sr.sprite = sprite;
        float s = width / Mathf.Max(0.01f, sprite.bounds.size.x);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
