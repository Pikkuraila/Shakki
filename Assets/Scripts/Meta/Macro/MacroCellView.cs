using UnityEngine;
using UnityEngine.UI;

public sealed class MacroCellView : MonoBehaviour
{
    [HideInInspector] public MacroBoardView board;
    [HideInInspector] public int Index;

    [Header("Visuals")]
    public Image backgroundImage;
    public Image iconImage;          // ← UUSI
    public Transform pieceAnchor;

    [Header("Colors")]
    public Color defaultColor = Color.gray;
    public Color currentColor = Color.white;
    public Color nextColor = Color.yellow;
    public Color battleColor = new Color(0.8f, 0.4f, 0.4f);
    public Color shopColor = new Color(0.4f, 0.8f, 0.4f);
    public Color restColor = new Color(0.4f, 0.4f, 0.8f);
    public Color randomColor = new Color(0.8f, 0.8f, 0.4f);
    public Color bossColor = new Color(0.6f, 0.2f, 0.6f);

    [Header("Icons")]
    public Sprite battleIcon;
    public Sprite shopIcon;
    public Sprite restIcon;
    public Sprite randomIcon;
    public Sprite bossIcon;
    public Sprite defaultIcon;

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

        int columns = board.map.columns;

        // --- RUUTUTYYPPIEN PÄÄVÄRI ---
        Color baseColor = defaultColor;

        switch (tile.type)
        {
            case MacroEventType.Battle: baseColor = battleColor; break;
            case MacroEventType.Shop: baseColor = shopColor; break;
            case MacroEventType.Rest: baseColor = restColor; break;
            case MacroEventType.RandomEvent: baseColor = randomColor; break;
            case MacroEventType.Boss: baseColor = bossColor; break;
            case MacroEventType.None:
            default: baseColor = defaultColor; break;
        }

        int myRow = Index / columns;
        int myCol = Index % columns;

        int currentRow = currentIndex >= 0 ? currentIndex / columns : -1;
        int currentCol = currentIndex >= 0 ? currentIndex % columns : -1;

        bool isCurrent = Index == currentIndex;

        bool isNextOption = false;
        if (currentRow >= 0)
        {
            isNextOption =
                (myRow == currentRow + 1) &&
                (Mathf.Abs(myCol - currentCol) <= 1);
        }

        // --- VÄRIKOROSTUS ---
        if (isCurrent)
            backgroundImage.color = currentColor;
        else if (isNextOption)
            backgroundImage.color = nextColor;
        else
            backgroundImage.color = baseColor;

        // --- IKONI ---
        if (iconImage != null)
        {
            Sprite s = defaultIcon;

            switch (tile.type)
            {
                case MacroEventType.Battle: s = battleIcon; break;
                case MacroEventType.Shop: s = shopIcon; break;
                case MacroEventType.Rest: s = restIcon; break;
                case MacroEventType.RandomEvent: s = randomIcon; break;
                case MacroEventType.Boss: s = bossIcon; break;
            }

            iconImage.sprite = s;
            iconImage.enabled = (s != null);
        }
    }
}
