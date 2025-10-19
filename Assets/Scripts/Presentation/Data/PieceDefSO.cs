using UnityEngine;
using System.Linq;
using Shakki.Core;

[CreateAssetMenu(fileName = "PieceDef", menuName = "Shakki/Piece")]
public class PieceDefSO : ScriptableObject
{
    [Header("Identity")]
    public string typeName = "Rook"; // t‰sm‰‰ Piece.TypeNameen

    [Header("Rules")]
    public MoveRuleSO[] rules;

    [Header("Tags")]
    [Tooltip("Erikoisominaisuudet kuten EnPassant, Flying, Boss jne.")]
    public PieceTag tags = PieceTag.None; // <-- uusi kentt‰

    [Header("Visuals")]
    public Sprite whiteSprite;
    public Sprite blackSprite;

    [Tooltip("Jos asetat t‰m‰n, k‰ytet‰‰n t‰t‰ prefabbia PiecePrefabin sijaan (valinnainen).")]
    public GameObject viewPrefabOverride;

    public Piece Build(string owner)
    {
        var built = rules?.Select(r => r.Build()).ToList()
                    ?? new System.Collections.Generic.List<IMoveRule>();
        return new Piece(owner, typeName, built, tags); // <-- v‰litet‰‰n tagit Pieceen
    }

    public Sprite GetSpriteFor(string owner)
        => owner == "white" ? whiteSprite : blackSprite;
}
