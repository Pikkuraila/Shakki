// Assets/Scripts/Meta/Player/PlayerData.cs
using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerData
{
    public int version = 1;
    public int coins = 0;

    // Omistettu sisältö
    public List<string> ownedPieceIds = new(); // esim. "Rook","Amazon","Alfil"
    public List<UpgradeInstance> upgrades = new();

    // ---- UUSI: Pre-battle loadout ----
    public List<LoadoutEntry> loadout = new(); // esim. {("Rook",2),("Bishop",2),("Knight",2),("Queen",1),("Pawn",8),"King" implisiittisesti}

    // ---- UUSI: Powerup-varasto (meta) ----
    public List<PowerupStack> powerups = new(); // esim. {"SwapPiece": 2, "PromotePawn":1}

    // (valinnainen) viimeisin siemen, run-statit jne.
    public string lastRunSeed;
    public int runsCompleted = 0;
}

[Serializable] public sealed class UpgradeInstance { public string upgradeId; public int level; }
[Serializable] public sealed class LoadoutEntry { public string pieceId; public int count; }
[Serializable] public sealed class PowerupStack { public string powerupId; public int count; }