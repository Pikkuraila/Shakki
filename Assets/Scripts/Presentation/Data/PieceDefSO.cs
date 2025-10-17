using UnityEngine;
using System.Linq;
using Shakki.Core;

[CreateAssetMenu(fileName = "PieceDef", menuName = "Shakki/Piece")]
public class PieceDefSO : ScriptableObject
{
    [Header("Identity")]
    public string typeName = "Rook";    // täsmää Piece.TypeNameen

    [Header("Rules")]
    public MoveRuleSO[] rules;

    [Header("Visuals")]
    public Sprite whiteSprite;          // valkoisen nappulan sprite
    public Sprite blackSprite;          // mustan nappulan sprite

    [Tooltip("Jos asetat tämän, käytetään tätä prefabbia PiecePrefabin sijaan (valinnainen).")]
    public GameObject viewPrefabOverride;

    public Piece Build(string owner)
    {
        var built = rules?.Select(r => r.Build()).ToList()
                    ?? new System.Collections.Generic.List<IMoveRule>();
        return new Piece(owner, typeName, built);
    }

    public Sprite GetSpriteFor(string owner)
        => owner == "white" ? whiteSprite : blackSprite;
}
