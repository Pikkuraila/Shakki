// Assets/Scripts/Meta/Player/LoadoutAssembler.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LoadoutAssembler
{
    // --- Pääpolku: suoraan sloteista ---
    public static EncounterSO BuildFromSlots(
    List<string> whiteSlots,
    SlotMapSO map,
    EnemySpec enemy)
    {
        if (map == null) throw new InvalidOperationException("SlotMapSO puuttuu.");
        if (map.whiteSlotCoords == null || map.whiteSlotCoords.Length == 0)
            throw new InvalidOperationException("SlotMapSO.whiteSlotCoords puuttuu.");

        var enc = ScriptableObject.CreateInstance<EncounterSO>();
        enc.relativeRanks = map.relativeRanks;

        // ✅ Alusta aina ennen Add-kutsuja
        if (enc.spawns == null)
            enc.spawns = new List<EncounterSO.Spawn>();

        // Valkoisen slotit → spawns 1:1 indeksillä
        int n = Math.Min(map.whiteSlotCoords.Length, whiteSlots?.Count ?? 0);
        for (int i = 0; i < n; i++)
        {
            var pid = whiteSlots[i];
            if (string.IsNullOrEmpty(pid)) continue;

            var c = map.GetWhiteCoordForIndex(i);
            enc.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = pid, x = c.x, y = c.y });
        }

        Debug.Log($"[Assembler] Added {enc.spawns.Count} spawns after WHITE slots.");

        ApplyEnemyPlan(enc, map, enemy);

        Debug.Log($"[Assembler] Added spawns total after ENEMY: {enc.spawns.Count}");
        return enc;
    }


    // Meta-listasta (pieceId → count)
    public static EncounterSO BuildFromEntries(
        List<LoadoutEntry> entries,
        SlotMapSO map,
        EnemySpec enemy,
        string implicitKingId = "King",
        int totalSlots = 16)
    {
        var slots = LoadoutModel.Expand(entries ?? new List<LoadoutEntry>(), totalSlots, implicitKingId);
        return BuildFromSlots(slots, map, enemy);
    }

    // PlayerDatasta – käyttää slotteja jos löytyy, muuten Expand metasta
    public static EncounterSO BuildFromPlayerData(
        PlayerData data,
        SlotMapSO map,
        EnemySpec enemy,
        string implicitKingId = "King",
        int totalSlots = 16)
    {
        List<string> slots =
            (data.loadoutSlots != null && data.loadoutSlots.Count == totalSlots)
            ? data.loadoutSlots
            : LoadoutModel.Expand(data.loadout ?? new List<LoadoutEntry>(), totalSlots, implicitKingId);

        return BuildFromSlots(slots, map, enemy);
    }

    // --- Vihollisen puoli ---
    static void ApplyEnemyPlan(EncounterSO enc, SlotMapSO map, EnemySpec e)
    {
        if (e == null) e = new EnemySpec { mode = EnemySpec.Mode.Classic };

        switch (e.mode)
        {
            case EnemySpec.Mode.Classic:
                if (map.relativeRanks) // 🔁 PÄÄTÄ tämän perusteella
                {
                    // Syötä REL-koordinat (white-perspektiivi), peilaus tapahtuu vasta EncounterLoaderissa
                    enc.spawns.Add(new EncounterSO.Spawn
                    {
                        owner = "black",
                        pieceId = e.blackKingId ?? "King",
                        x = 4,
                        y = 0            // 👈 REL takarivi
                    });
                    enc.fillBlackPawnsAtY = true;
                    enc.blackPawnsY = 1;  // REL sotilasrivi
                    enc.relativeRanks = true; // turvalukko
                }
                else
                {
                    // ABS koordit (ei peilausta)
                    enc.spawns.Add(new EncounterSO.Spawn
                    {
                        owner = "black",
                        pieceId = e.blackKingId ?? "King",
                        x = 4,
                        y = 7            // ABS takarivi 8x8:ssa
                    });
                    enc.fillBlackPawnsAtY = true;
                    enc.blackPawnsY = 6;  // ABS sotilasrivi
                    enc.relativeRanks = false;
                }
                break;

                // ... muut keissit ennallaan ...
        }
    }

}
