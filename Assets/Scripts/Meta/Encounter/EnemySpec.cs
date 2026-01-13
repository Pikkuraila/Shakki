// Assets/Scripts/Meta/Encounter/EnemySpec.cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class EnemySpec
{
    public enum Mode { Classic, PresetEncounter, Slots }

    public Mode mode = Mode.Classic;

    // Preset
    public EncounterSO preset;

    // Slots: lista pieceId:itä jotka droppaat mustalle
    public List<string> blackSlots;

    // ✅ Drop-asetukset
    [Header("Drop rules")]
    public bool useDropPlacement = true;
    public int forbidWhiteAndAllyRows = 3; // y=0..2 kielletty (white+ally)

    [Tooltip("0 = tasainen, 2 = vahva painotus mustan päätyyn")]
    [Range(0f, 4f)] public float backBiasPower = 2f;

    [Header("Fallback fill")]
    public bool fallbackFillBlackPawnsRow = false;  // vain jos haluat
    public int fallbackBlackPawnsRelY = 1;          // REL y=1 => abs y=6 8x8:ssa
}
