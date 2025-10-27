// Assets/Scripts/Meta/Defs/SlotMapSO.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SlotMap", menuName = "Shakki/SlotMap")]
public class SlotMapSO : ScriptableObject
{
    public bool relativeRanks = true;

    [Tooltip("Täsmälleen 16 koordia: indeksit 0..7 = backline, 8..15 = pawnline (tai haluamasi järjestys)")]
    public Vector2Int[] whiteSlotCoords = new Vector2Int[16];

    public Vector2Int[] blackSlotCoords;

    // NEW: apuri
    public Vector2Int GetWhiteCoordForIndex(int i)
    {
        if (whiteSlotCoords == null || i < 0 || i >= whiteSlotCoords.Length)
            throw new System.IndexOutOfRangeException($"SlotMapSO white index {i} out of range");
        return whiteSlotCoords[i];
    }

    // NEW: validointi (voit kutsua esim. Awake/OnValidate)
    public void Validate()
    {
        if (whiteSlotCoords == null || whiteSlotCoords.Length != 16)
            Debug.LogError("[SlotMapSO] whiteSlotCoords must be length 16.");
    }

    private void OnValidate() => Validate();

    public void ValidateBounds(int width, int height)
    {
        if (whiteSlotCoords == null || whiteSlotCoords.Length != 16)
        {
            Debug.LogError("[SlotMapSO] whiteSlotCoords must be length 16.");
            return;
        }

        var seen = new HashSet<Vector2Int>();
        for (int i = 0; i < whiteSlotCoords.Length; i++)
        {
            var c = whiteSlotCoords[i];
            if (c.x < 0 || c.x >= width || c.y < 0 || c.y >= height)
                Debug.LogWarning($"[SlotMapSO] whiteSlotCoords[{i}] = ({c.x},{c.y}) out of {width}x{height}");
            if (!seen.Add(c))
                Debug.LogWarning($"[SlotMapSO] Duplicate coord ({c.x},{c.y}) at index {i}");
        }
    }
}
