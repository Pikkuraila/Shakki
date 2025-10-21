using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/SlotMap", fileName = "SlotMap")]
public sealed class SlotMapSO : ScriptableObject
{
    public Vector2Int[] backline = { new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0), new(6, 0), new(7, 0) };
    public Vector2Int[] pawnline = { new(0, 1), new(1, 1), new(2, 1), new(3, 1), new(4, 1), new(5, 1), new(6, 1), new(7, 1) };
}
