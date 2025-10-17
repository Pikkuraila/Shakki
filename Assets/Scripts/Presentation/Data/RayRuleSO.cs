using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "RayRule", menuName = "Shakki/Rules/Ray")]
public class RayRuleSO : MoveRuleSO
{
    public Vector2Int[] directions;
    public int maxRange = int.MaxValue;
    public bool captureOnly = false;
    public bool nonCaptureOnly = false;

    public override IMoveRule Build()
    {
        var arr = new (int, int)[directions.Length];
        for (int i = 0; i < directions.Length; i++)
            arr[i] = (directions[i].x, directions[i].y);

        return new RayRule(arr, maxRange, captureOnly, nonCaptureOnly);
    }
}
