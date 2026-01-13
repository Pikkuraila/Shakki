// Assets/Scripts/Core/PieceDefSO.cs (tai missä tää nyt on)
using UnityEngine;
using System.Linq;
using Shakki.Core;

[CreateAssetMenu(fileName = "PieceDef", menuName = "Shakki/Piece")]
public class PieceDefSO : ScriptableObject
{
    [Header("Identity")]
    public string typeName = "Rook";

    [Header("Economy")]
    [Min(0)] public int cost = 1;   // ✅ uusi

    [Header("Rules")]
    public MoveRuleSO[] rules;

    [Header("Tags")]
    public PieceTag tags = PieceTag.None;

    [Header("Visuals")]
    public Sprite whiteSprite;
    public Sprite blackSprite;

    public GameObject viewPrefabOverride;

    public Piece Build(string owner)
    {
        var built = rules?.Select(r => r.Build()).ToList()
                    ?? new System.Collections.Generic.List<IMoveRule>();
        return new Piece(owner, typeName, built, tags);
    }

    public Sprite GetSpriteFor(string owner)
        => owner == "white" ? whiteSprite : blackSprite;
}
