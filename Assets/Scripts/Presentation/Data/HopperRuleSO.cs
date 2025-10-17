using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "HopperRule", menuName = "Shakki/Rules/Hopper")]
public class HopperRuleSO : MoveRuleSO
{
    [Tooltip("Suuntavektorit esim. tornille (1,0),(0,1),(-1,0),(0,-1)")]
    public Vector2Int[] directions = {
        new Vector2Int(1,0),
        new Vector2Int(-1,0),
        new Vector2Int(0,1),
        new Vector2Int(0,-1)
    };

    [Header("Asetukset")]
    public int hopDistance = 1;
    public bool captureOnlyAfterJump = false;
    public bool canLandEmpty = true;

    public override IMoveRule Build()
    {
        var arr = new (int, int)[directions.Length];
        for (int i = 0; i < directions.Length; i++)
            arr[i] = (directions[i].x, directions[i].y);

        return new HopperRule(arr, hopDistance, captureOnlyAfterJump, canLandEmpty);
    }
}
