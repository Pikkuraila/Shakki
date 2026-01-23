// Assets/Scripts/Core/PieceDefSO.cs
using UnityEngine;
using System.Linq;
using Shakki.Core;

[CreateAssetMenu(fileName = "PieceDef", menuName = "Shakki/Piece")]
public class PieceDefSO : ScriptableObject
{
    [Header("Identity")]
    public string typeName = "Rook";

    [Header("Economy")]
    [Min(0)] public int cost = 1;

    [Header("Rules")]
    public MoveRuleSO[] rules;

    [Header("Tags")]
    public PieceTag tags = PieceTag.None;

    [Header("Visuals (Board)")]
    public Sprite whiteSprite;
    public Sprite blackSprite;

    public GameObject viewPrefabOverride;

    [Header("Inspect (Portrait)")]
    [Tooltip("Erillinen portrait-kuva inspect-paneeliin. Jos tyhjä, fallbackaa whiteSpriteen.")]
    public Sprite portraitSprite;

    public Piece Build(string owner)
    {
        var built = rules?.Select(r => r.Build()).ToList()
                    ?? new System.Collections.Generic.List<IMoveRule>();
        return new Piece(owner, typeName, built, tags);
    }

    public Sprite GetSpriteFor(string owner)
        => owner == "white" ? whiteSprite : blackSprite;

    // ✅ Inspect-paneelin ensisijainen kuva
    public Sprite GetPortrait()
        => portraitSprite != null ? portraitSprite : whiteSprite;
}
