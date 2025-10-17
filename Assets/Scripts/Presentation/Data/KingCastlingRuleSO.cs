using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "CastlingRule", menuName = "Shakki/Rules/Castling")]
public class KingCastlingRuleSO : MoveRuleSO
{
    public override IMoveRule Build() => new CastlingRule();
}
