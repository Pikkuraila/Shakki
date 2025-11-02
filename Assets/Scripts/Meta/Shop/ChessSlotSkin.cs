using UnityEngine;
using UnityEngine.UI;

public sealed class ChessSlotSkin : MonoBehaviour
{
    public Sprite lightSprite;
    public Sprite darkSprite;

    [Tooltip("Jos 0 → haetaan GridLayoutGroup.constraintCount; muuten käytä tätä.")]
    public int columnsOverride = 0;

    [Tooltip("Jos true → älä korvaa olemassa olevaa spriteä nulliksi, jos light/dark puuttuu.")]
    public bool keepExistingIfMissing = true;

    [HideInInspector] public int index;

    public void Apply()
    {
        var img = GetComponent<Image>();
        if (img == null) return;

        int cols = columnsOverride;
        if (cols <= 0)
        {
            var grid = GetComponentInParent<GridLayoutGroup>();
            if (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                cols = grid.constraintCount;
            if (cols <= 0) cols = 8;
        }

        bool isDark = ((index % cols) + (index / cols)) % 2 == 1;
        var target = isDark ? darkSprite : lightSprite;

        if (target != null)
        {
            img.sprite = target;
            img.type = Image.Type.Sliced;   // 9-slice → muista Border; muuten Simple
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else
        {
            if (keepExistingIfMissing && img.sprite != null)
            {
                // Jätä nykyinen sprite koskematta
            }
            else
            {
                // Näkyvä fallback, vaikka sprite puuttuisi
                img.sprite = null;
                img.type = Image.Type.Simple;
                img.color = new Color(1f, 1f, 1f, 0.15f); // kevyt harmaa paneeli
                img.enabled = true;
            }
        }
    }

    void OnEnable() { Apply(); }
}
