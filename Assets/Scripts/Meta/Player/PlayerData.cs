// Assets/Scripts/Meta/Player/PlayerData.cs
using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerData
{
    public int version = 1;
    public int coins = 0;

    public List<string> ownedPieceIds = new();
    public List<UpgradeInstance> upgrades = new();

    // META (johdettu/kompressoitu): “montako kutakin”
    public List<LoadoutEntry> loadout = new();

    // UI/TOTUUS: 16 paikkaa, tyhjä = ""
    public List<string> loadoutSlots = new();   // pituus 16

    public List<PowerupStack> powerups = new();

    public string lastRunSeed;
    public int runsCompleted = 0;

    public List<string> inventoryIds = new();          // inventory-gridin sisältö
    public List<List<string>> slotPowerups = new();    // per-slot liitetyt powerup-id:t

    public int macroIndex; // nykyinen sijainti makrolaudalla (0–15)

    // --- DIALOGUE / NPC STATE ---
    public List<NpcState> npcStates = new();    // per-npc alignment & met-count
    public List<string> storyFlags = new();     // esim. "visited_rest", "hermit_helped"
    public string lastMacroEvent;               // esim "Rest", "Shop", "Alchemist"

}

[Serializable] public sealed class UpgradeInstance { public string upgradeId; public int level; }
[Serializable] public sealed class LoadoutEntry { public string pieceId; public int count; }
[Serializable] public sealed class PowerupStack { public string powerupId; public int count; }

[Serializable]
public sealed class NpcState
{
    public string npcId;
    public int alignment;   // esim -100..100
    public int timesMet;
}
