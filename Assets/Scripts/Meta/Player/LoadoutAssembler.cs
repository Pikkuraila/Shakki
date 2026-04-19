using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LoadoutAssembler
{
    public static EncounterSO BuildFromSlotsDrop(
        List<string> whiteSlots,
        SlotMapSO map,
        EnemySpec enemy,
        int boardWidth,
        int boardHeight)
    {
        if (map == null) throw new InvalidOperationException("SlotMapSO puuttuu.");
        if (map.whiteSlotCoords == null || map.whiteSlotCoords.Length == 0)
            throw new InvalidOperationException("SlotMapSO.whiteSlotCoords puuttuu.");

        if (enemy == null) enemy = new EnemySpec { mode = EnemySpec.Mode.Classic };

        var enc = ScriptableObject.CreateInstance<EncounterSO>();

        if (enc.spawns == null)
            enc.spawns = new List<EncounterSO.Spawn>();

        int n = Math.Min(map.whiteSlotCoords.Length, whiteSlots?.Count ?? 0);
        for (int i = 0; i < n; i++)
        {
            var pid = whiteSlots[i];
            if (string.IsNullOrEmpty(pid)) continue;

            var c = map.GetWhiteCoordForIndex(i);
            enc.spawns.Add(new EncounterSO.Spawn
            {
                owner = "white",
                pieceId = pid,
                x = c.x,
                y = c.y
            });
        }

        ApplyEnemyPlanDrop(enc, enemy, boardWidth, boardHeight);

        return enc;
    }

    public static EncounterSO BuildFromPlayerDataDrop(
        PlayerData data,
        SlotMapSO map,
        EnemySpec enemy,
        int boardWidth,
        int boardHeight,
        string implicitKingId = "King",
        int totalSlots = 16)
    {
        if (map == null) throw new InvalidOperationException("SlotMapSO puuttuu.");
        if (map.whiteSlotCoords == null || map.whiteSlotCoords.Length == 0)
            throw new InvalidOperationException("SlotMapSO.whiteSlotCoords puuttuu.");

        if (enemy == null) enemy = new EnemySpec { mode = EnemySpec.Mode.Classic };

        var enc = ScriptableObject.CreateInstance<EncounterSO>();
        if (enc.spawns == null)
            enc.spawns = new List<EncounterSO.Spawn>();

        enc.spawns.AddRange(BuildWhiteSpawnsFromPlayerData(data, map, totalSlots, implicitKingId));
        ApplyEnemyPlanDrop(enc, enemy, boardWidth, boardHeight);
        return enc;
    }

    private static List<EncounterSO.Spawn> BuildWhiteSpawnsFromPlayerData(
        PlayerData data,
        SlotMapSO map,
        int totalSlots,
        string implicitKingId)
    {
        var spawns = new List<EncounterSO.Spawn>();
        if (data == null)
            return spawns;

        bool hasInstanceModel =
            data.version >= PlayerInstanceSync.CurrentDataVersion &&
            data.pieceInstances != null &&
            data.pieceInstances.Count > 0 &&
            data.loadoutSlotInstances != null &&
            data.loadoutSlotInstances.Count == totalSlots;

        if (hasInstanceModel)
        {
            foreach (var slot in data.loadoutSlotInstances)
            {
                if (slot == null || slot.slotIndex < 0 || slot.slotIndex >= totalSlots)
                    continue;

                var instance = PlayerInstanceSync.FindAliveInstance(data, slot.pieceInstanceId);
                var pieceId = PlayerInstanceSync.GetLegacyPieceId(instance);
                if (instance == null || string.IsNullOrEmpty(pieceId))
                    continue;

                var c = map.GetWhiteCoordForIndex(slot.slotIndex);
                spawns.Add(new EncounterSO.Spawn
                {
                    owner = "white",
                    pieceId = pieceId,
                    pieceInstanceId = instance.instanceId,
                    x = c.x,
                    y = c.y
                });
            }

            return spawns;
        }

        List<string> slots = BuildWhiteSlotsFromPlayerData(data, totalSlots, implicitKingId);
        int n = Math.Min(map.whiteSlotCoords.Length, slots?.Count ?? 0);
        for (int i = 0; i < n; i++)
        {
            var pid = slots[i];
            if (string.IsNullOrEmpty(pid)) continue;

            var c = map.GetWhiteCoordForIndex(i);
            spawns.Add(new EncounterSO.Spawn
            {
                owner = "white",
                pieceId = pid,
                x = c.x,
                y = c.y
            });
        }

        return spawns;
    }

    private static List<string> BuildWhiteSlotsFromPlayerData(PlayerData data, int totalSlots, string implicitKingId)
    {
        if (data == null)
            return LoadoutModel.Expand(new List<LoadoutEntry>(), totalSlots, implicitKingId);

        bool hasInstanceModel =
            data.version >= PlayerInstanceSync.CurrentDataVersion &&
            data.pieceInstances != null &&
            data.pieceInstances.Count > 0 &&
            data.loadoutSlotInstances != null &&
            data.loadoutSlotInstances.Count == totalSlots;

        if (hasInstanceModel)
        {
            var slots = Enumerable.Repeat(string.Empty, totalSlots).ToList();
            foreach (var slot in data.loadoutSlotInstances)
            {
                if (slot == null || slot.slotIndex < 0 || slot.slotIndex >= totalSlots)
                    continue;

                var instance = PlayerInstanceSync.FindAliveInstance(data, slot.pieceInstanceId);
                slots[slot.slotIndex] = instance != null ? PlayerInstanceSync.GetLegacyPieceId(instance) : string.Empty;
            }

            return slots;
        }

        PlayerInstanceSync.SyncInstancesFromLegacy(data, totalSlots);

        if (data.loadoutSlotInstances != null && data.loadoutSlotInstances.Count == totalSlots)
        {
            var slots = Enumerable.Repeat(string.Empty, totalSlots).ToList();
            foreach (var slot in data.loadoutSlotInstances)
            {
                if (slot == null || slot.slotIndex < 0 || slot.slotIndex >= totalSlots)
                    continue;

                var instance = PlayerInstanceSync.FindAliveInstance(data, slot.pieceInstanceId);
                slots[slot.slotIndex] = instance != null ? PlayerInstanceSync.GetLegacyPieceId(instance) : string.Empty;
            }

            return slots;
        }

        return (data.loadoutSlots != null && data.loadoutSlots.Count == totalSlots)
            ? data.loadoutSlots
            : LoadoutModel.Expand(data.loadout ?? new List<LoadoutEntry>(), totalSlots, implicitKingId);
    }

    static void ApplyEnemyPlanDrop(EncounterSO enc, EnemySpec e, int w, int h)
    {
        if (e == null) return;

        var usedAbs = new HashSet<(int x, int y)>();

        foreach (var sp in enc.spawns)
            usedAbs.Add((sp.x, sp.y));

        switch (e.mode)
        {
            case EnemySpec.Mode.PresetEncounter:
                if (e.preset != null && e.preset.spawns != null)
                {
                    foreach (var sp in e.preset.spawns)
                    {
                        if (string.IsNullOrEmpty(sp.owner) || string.IsNullOrEmpty(sp.pieceId))
                            continue;

                        if (!sp.owner.Equals("black", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (sp.x < 0 || sp.x >= w || sp.y < 0 || sp.y >= h)
                            continue;

                        if (sp.y < e.forbidWhiteAndAllyRows)
                            continue;

                        if (usedAbs.Add((sp.x, sp.y)))
                        {
                            enc.spawns.Add(new EncounterSO.Spawn
                            {
                                owner = "black",
                                pieceId = sp.pieceId,
                                x = sp.x,
                                y = sp.y
                            });
                        }
                    }
                }
                break;

            case EnemySpec.Mode.Slots:
                DropBlackSlots(enc, e, w, h, usedAbs);
                break;

            case EnemySpec.Mode.Classic:
            default:
                break;
        }
    }

    static void DropBlackSlots(EncounterSO enc, EnemySpec e, int w, int h, HashSet<(int x, int y)> usedAbs)
    {
        if (e.blackSlots == null || e.blackSlots.Count == 0)
            return;

        int minAbsYAllowed = Mathf.Clamp(e.forbidWhiteAndAllyRows, 0, h);
        int maxAbsYAllowed = h - 1;

        float biasPow = Mathf.Max(0f, e.backBiasPower);
        int safety = 0;

        foreach (var pid in e.blackSlots)
        {
            if (string.IsNullOrEmpty(pid)) continue;

            bool placed = false;
            for (int tries = 0; tries < 50; tries++)
            {
                safety++;
                if (safety > 5000) break;

                int x = UnityEngine.Random.Range(0, w);

                float r = UnityEngine.Random.value;
                float biased = (biasPow <= 0f) ? r : Mathf.Pow(r, biasPow);

                int y = Mathf.Clamp(
                    Mathf.FloorToInt(minAbsYAllowed + biased * (maxAbsYAllowed - minAbsYAllowed + 1)),
                    minAbsYAllowed,
                    maxAbsYAllowed
                );

                if (usedAbs.Contains((x, y))) continue;

                usedAbs.Add((x, y));
                enc.spawns.Add(new EncounterSO.Spawn
                {
                    owner = "black",
                    pieceId = pid,
                    x = x,
                    y = y
                });
                placed = true;
                break;
            }

            if (!placed)
                Debug.LogWarning($"[Assembler] Could not place black '{pid}' (no free cell after tries).");
        }
    }
}
