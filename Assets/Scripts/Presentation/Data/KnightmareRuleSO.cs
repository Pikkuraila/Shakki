using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "KnightmareRule", menuName = "Shakki/Rules/KnightmareRay")]
public class KnightmareRuleSO : MoveRuleSO
{
    public override IMoveRule Build() => new KnightmareRule();
}
