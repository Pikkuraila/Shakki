using UnityEngine;
using UnityEngine.UI;

public sealed class MacroCellView : MonoBehaviour
{
    [HideInInspector] public MacroBoardView board;
    [HideInInspector] public int Index;

    [Header("Visuals")]
    public Image backgroundImage;
    public Image iconImage;
    public Transform pieceAnchor;

    [Header("Icons")]
    [SerializeField] private MacroEventIconsSO icons;
    [SerializeField] private Sprite defaultIcon;

    public void Setup(MacroBoardView board, int index)
    {
        this.board = board;
        this.Index = index;
    }

    public void Refresh(MacroTileDef tile, int currentIndex)
    {
        if (backgroundImage == null)
        {
            Debug.LogError($"[MacroCellView] {name} missing backgroundImage ref");
            return;
        }

        if (board == null || board.map == null) return;

        // ✅ Väritys jätetään tile-graffan/taustan hoidettavaksi:
        // backgroundImage.color = (pidä nykyinen tai anna prefab/skin hoitaa)

        if (iconImage != null)
        {
            Sprite s = null;

            if (icons != null)
                s = icons.GetIconOrNull(tile.type);

            if (s == null)
                s = defaultIcon;

            iconImage.sprite = s;
            iconImage.enabled = (s != null);
        }
    }
}
