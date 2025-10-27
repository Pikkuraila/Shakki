// Assets/Scripts/Meta/Encounter/EnemySpec.cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class EnemySpec
{
    public enum Mode { Classic, PresetEncounter, Slots }

    public Mode mode = Mode.Classic;

    // Classic
    public string whiteKingId = "King";
    public string blackKingId = "King";
    public bool ensureWhiteKing = true;

    // Preset
    public EncounterSO preset;

    // Slots (valinnainen): jos haluat mustalle oman slottilistan
    public List<string> blackSlots;
}
