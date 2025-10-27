using UnityEngine;
using UnityEngine.UI;

public sealed class ChessSlotSkin : MonoBehaviour
{
    public Sprite lightSprite;
    public Sprite darkSprite;

    [HideInInspector] public int index; // Grid t‰ytt‰‰ t‰m‰n

    public void Apply()
    {
        var img = GetComponent<Image>();
        if (img == null) return;
        bool isDark = ((index % 8) + (index / 8)) % 2 == 1; // 8x2: shakittainen vuorotus
        img.sprite = isDark ? darkSprite : lightSprite;
        img.type = Image.Type.Sliced; // jos 9-slice; muuten Simple
        img.preserveAspect = true;
    }
}