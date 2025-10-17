using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "EnPassantRule", menuName = "Shakki/Rules/Pawn En Passant")]
public class EnPassantRuleSO : MoveRuleSO
{
    public int directionY = +1; // white:+1, black:-1
    public override IMoveRule Build() => new EnPassantRule(directionY);
}
