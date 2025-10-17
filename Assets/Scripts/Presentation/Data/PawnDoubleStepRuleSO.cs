using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "PawnDoubleStepRule", menuName = "Shakki/Rules/Pawn Double Step")]
public class PawnDoubleStepRuleSO : MoveRuleSO
{
    [Tooltip("Ei käytössä – suunta/starttirivi päätellään Ownerista.")]
    public int directionY = +1;
    public int startRank = 1;

    public override IMoveRule Build() => new PawnDoubleStepRule();
}
