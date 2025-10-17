using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "CannonRule", menuName = "Shakki/Rules/CannonRule")]
public class CannonRuleSO : MoveRuleSO
{
    public override IMoveRule Build() => new CannonRule();
}
