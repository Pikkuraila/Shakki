using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "PawnCoreRule", menuName = "Shakki/Rules/Pawn Core")]
public class PawnCoreRuleSO : MoveRuleSO
{
    [Tooltip("Ei käytössä – suunta päätellään nappulan Ownerista.")]
    public int directionY = +1;

    public override IMoveRule Build() => new PawnCoreRule();
}