// Assets/Scripts/Meta/Player/LoadoutAssembler.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LoadoutAssembler
{
    // --- DROP-POLKU: tarvitsee laudan koon (width/height) jotta voidaan rajoittaa rivit ---
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
        enc.relativeRanks = true; // pidetään aina true tässä systeemissä

        if (enc.spawns == null)
            enc.spawns = new List<EncounterSO.Spawn>();

        // 1) Valkoisen slotit → spawns (kuten ennen)
        int n = Math.Min(map.whiteSlotCoords.Length, whiteSlots?.Count ?? 0);
        for (int i = 0; i < n; i++)
        {
            var pid = whiteSlots[i];
            if (string.IsNullOrEmpty(pid)) continue;

            var c = map.GetWhiteCoordForIndex(i);
            enc.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = pid, x = c.x, y = c.y });
        }

        // 2) Musta puoli
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
        List<string> slots =
            (data.loadoutSlots != null && data.loadoutSlots.Count == totalSlots)
            ? data.loadoutSlots
            : LoadoutModel.Expand(data.loadout ?? new List<LoadoutEntry>(), totalSlots, implicitKingId);

        return BuildFromSlotsDrop(slots, map, enemy, boardWidth, boardHeight);
    }

    // -----------------------------
    // Enemy drop placement
    // -----------------------------
    static void ApplyEnemyPlanDrop(EncounterSO enc, EnemySpec e, int w, int h)
    {
        if (e == null) return;

        // rakkaudesta selkeyteen: kerätään jo varatut ABS-koordit
        var usedAbs = new HashSet<(int x, int y)>();

        // white spawns ABS (white ei peilaudu relativeRanksissa)
        foreach (var sp in enc.spawns)
        {
            if (sp.owner == "white")
                usedAbs.Add((sp.x, sp.y));
            else if (sp.owner == "black")
            {
                // jos mustaa olisi jo presetistä, konvertoi ABS:ksi
                int absY = (h - 1) - sp.y;
                usedAbs.Add((sp.x, absY));
            }
        }

        switch (e.mode)
        {
            case EnemySpec.Mode.PresetEncounter:
                // Jos preset-encounterilla halutaan mustat “sellaisenaan”
                if (e.preset != null && e.preset.spawns != null)
                {
                    foreach (var sp in e.preset.spawns)
                    {
                        if (string.IsNullOrEmpty(sp.owner) || string.IsNullOrEmpty(sp.pieceId)) continue;
                        if (!sp.owner.Equals("black", StringComparison.OrdinalIgnoreCase)) continue;

                        // oletetaan että presetin black spawns on REL (kuten muu systeemi)
                        var rel = new EncounterSO.Spawn { owner = "black", pieceId = sp.pieceId, x = sp.x, y = sp.y };

                        int absY = (h - 1) - rel.y;
                        if (absY < e.forbidWhiteAndAllyRows) continue; // älä riko safe zonea

                        if (usedAbs.Add((rel.x, absY)))
                            enc.spawns.Add(rel);
                    }
                }
                break;

            case EnemySpec.Mode.Slots:
                DropBlackSlots(enc, e, w, h, usedAbs);
                break;

            case EnemySpec.Mode.Classic:
            default:
                // Classic fallback: esim. king + pawn-row (mut tää ei ole budjettipolku)
                // jätetään tää ennalleen tai tee oma.
                break;
        }

        // Fallback-fill: vain pawn-row, jos pyydetty
        if (e.fallbackFillBlackPawnsRow)
        {
            enc.fillBlackPawnsAtY = true;
            enc.blackPawnsY = e.fallbackBlackPawnsRelY;
        }
    }

    static void DropBlackSlots(EncounterSO enc, EnemySpec e, int w, int h, HashSet<(int x, int y)> usedAbs)
    {
        if (e.blackSlots == null || e.blackSlots.Count == 0)
        {
            // jos tyhjä, voidaan halutessa fallback-fillata pawnit
            return;
        }

        int forbidRows = Mathf.Clamp(e.forbidWhiteAndAllyRows, 0, h);     // esim 3
        int maxAbsYAllowed = h - 1;                                       // yAbs max
        int minAbsYAllowed = forbidRows;                                  // yAbs >= 3

        // REL-alue mustalle on yRel <= (h-1 - minAbsYAllowed)
        int maxRelY = (h - 1) - minAbsYAllowed;                           // 8x8 + forbid=3 => 4
        if (maxRelY < 0) maxRelY = 0;

        float biasPow = Mathf.Max(0f, e.backBiasPower);

        int safety = 0;

        foreach (var pid in e.blackSlots)
        {
            if (string.IsNullOrEmpty(pid)) continue;

            // etsitään vapaa paikka
            bool placed = false;
            for (int tries = 0; tries < 50; tries++)
            {
                safety++;
                if (safety > 5000) break;

                int x = UnityEngine.Random.Range(0, w);

                // bias: pow(r, p) painottaa lähelle 0 kun p>1
                float r = UnityEngine.Random.value;
                float biased = (biasPow <= 0f) ? r : Mathf.Pow(r, biasPow);
                int yRel = Mathf.Clamp(Mathf.FloorToInt(biased * (maxRelY + 1)), 0, maxRelY);

                int yAbs = (h - 1) - yRel; // EncounterLoaderin peilaus

                if (yAbs < minAbsYAllowed) continue;
                if (yAbs > maxAbsYAllowed) continue;

                if (usedAbs.Contains((x, yAbs))) continue;

                usedAbs.Add((x, yAbs));
                enc.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = pid, x = x, y = yRel });
                placed = true;
                break;
            }

            if (!placed)
            {
                // Ei löydetty tilaa -> ignoorataan tämä pid (tai halutessa triggeröidään fallback myöhemmin)
                Debug.LogWarning($"[Assembler] Could not place black '{pid}' (no free cell after tries).");
            }
        }
    }
}
