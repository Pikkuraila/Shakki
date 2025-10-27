// Assets/Scripts/Meta/Encounter/EncounterLoader.cs
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Shakki.Core;   // GameState, Coord, Piece, IMoveRule

public static class EncounterLoader
{
    public static void Apply(GameState s, EncounterSO enc, GameCatalogSO catalog)
    {
        Debug.Log($"[EL] relativeRanks={enc.relativeRanks} fillBlackPawnsAtY={enc.fillBlackPawnsAtY} blackPawnsY={enc.blackPawnsY} board={s.Width}x{s.Height}");

        if (enc.spawns == null || enc.spawns.Count == 0)
        {
            Debug.LogWarning("[EL] No spawns provided.");
        }
        else
        {
            Debug.Log("[EL] spawns-in (rel): " + string.Join(", ",
                enc.spawns.Select(sp => $"{sp.owner}:{sp.pieceId}@({sp.x},{sp.y})")));

            // Turvallinen tarkistus ilman '?.'
            var firstOwner = enc.spawns[0].owner;
            if (string.IsNullOrEmpty(firstOwner))
                Debug.LogWarning("[EL] First spawn owner is null/empty — check EnemyAssembler & LoadoutAssembler owners!");
        }




        if (s == null) { Debug.LogError("[EncounterLoader] GameState is null"); return; }
        if (enc == null) { Debug.LogError("[EncounterLoader] EncounterSO is null"); return; }
        if (catalog == null) { Debug.LogError("[EncounterLoader] GameCatalogSO is null"); return; }

        // 0) Tyhjennä lauta turvallisesti GameState-APIn kautta
        ClearBoardBySettingNulls(s);



        // 1) Spawnaa kaikki määritellyt olennot
        foreach (var sp in enc.spawns)
        {
            if (string.IsNullOrEmpty(sp.owner) || string.IsNullOrEmpty(sp.pieceId))
                continue;

            var def = catalog.GetPieceById(sp.pieceId);
            if (def == null)
            {
                Debug.LogWarning($"[EncounterLoader] Missing PieceDefSO for '{sp.pieceId}'");
                continue;
            }

            var c = ToAbsoluteCoord(s, enc, sp.owner, sp.x, sp.y);
            if (!s.InBounds(c))
            {
                Debug.LogWarning($"[EncounterLoader] OOB spawn {sp.owner}:{sp.pieceId} at {c.X},{c.Y}");
                continue;
            }

            if (s.Get(c) != null)
            {
                Debug.LogWarning($"[EncounterLoader] Cell occupied at {c.X},{c.Y} -> skip {sp.owner}:{sp.pieceId}");
                continue;
            }

            var piece = CreatePieceFromDef(sp.owner, def);
            if (piece == null)
            {
                Debug.LogError($"[EncounterLoader] Could not construct Piece for '{def?.typeName}'");
                continue;
            }

            s.Set(c, piece);
        }

        // 2) Täytä mustan sotilasrivi tarvittaessa
        if (enc.fillBlackPawnsAtY)
        {
            int absY = enc.relativeRanks ? (s.Height - 1 - enc.blackPawnsY) : enc.blackPawnsY;
            var pawnDef = catalog.GetPieceById("Pawn");
            if (pawnDef == null)
                Debug.LogWarning("[EncounterLoader] No PieceDefSO found for 'Pawn' when filling black pawns row.");

            for (int x = 0; x < s.Width; x++)
            {
                var c = new Coord(x, absY);
                if (!s.InBounds(c)) continue;
                if (s.Get(c) != null) continue;

                var pawn = pawnDef != null ? CreatePieceFromDef("black", pawnDef)
                                           : new Piece("black", "Pawn", Array.Empty<IMoveRule>());
                s.Set(c, pawn);
            }
        }
    }

    // --- Helpers ---

    private static void ClearBoardBySettingNulls(GameState s)
    {
        foreach (var c in s.AllCoords())
            s.Set(c, null);
    }

    /// Muuntaa (x,y) absoluuttiseksi koordiksi huomioiden relativeRanks.
    private static Coord ToAbsoluteCoord(GameState s, EncounterSO enc, string owner, int x, int y)
    {
        if (!enc.relativeRanks) return new Coord(x, y);
        if (!string.IsNullOrEmpty(owner) && owner.Equals("black", StringComparison.OrdinalIgnoreCase))
            return new Coord(x, (s.Height - 1) - y);
        return new Coord(x, y);
    }

    /// Luo Piece-instanssin PieceDefSO:sta.
    /// Yrittää rakentaa IMoveRule[] def.rules:ista (jos MoveRuleSO:lla on Build/ToRule-metodi).
    /// Muussa tapauksessa käyttää tyhjää sääntölistaa — jolloin pelin on syytä käyttää IRulesResolveria siirtogeneraatiolle.
    private static Piece CreatePieceFromDef(string owner, PieceDefSO def)
    {
        var rules = TryBuildRules(def) ?? Array.Empty<IMoveRule>();
        var piece = new Piece(owner, def.typeName, rules)
        {
            HasMoved = false
        };
        return piece;
    }

    /// Rakennetaan IMoveRule[] PieceDefSO.rules:ista, jos mahdollista.
    private static IMoveRule[] TryBuildRules(PieceDefSO def)
    {
        if (def == null || def.rules == null || def.rules.Length == 0)
            return Array.Empty<IMoveRule>();

        var list = def.rules
            .Where(r => r != null)
            .Select(BuildRuleSafe)
            .Where(r => r != null)
            .Cast<IMoveRule>()
            .ToArray();

        return list;
    }

    /// Kutsuu yleisimpiä tehtaanimiä: Build(), ToRule(), Create(), Instantiate()
    /// Palauttaa null, jos mikään ei onnistu.
    private static IMoveRule BuildRuleSafe(ScriptableObject ruleSO)
    {
        if (ruleSO == null) return null;

        var t = ruleSO.GetType();
        // Yleisimmät nimet, parametriton ja palauttaa IMoveRule
        var methodNames = new[] { "Build", "ToRule", "Create", "Instantiate" };

        foreach (var name in methodNames)
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (m == null) continue;

            try
            {
                var res = m.Invoke(ruleSO, null);
                if (res is IMoveRule rule) return rule;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EncounterLoader] {t.Name}.{name} failed: {e.Message}");
            }
        }

        // Jos SO:lla on suora property "Rule" (IMoveRule) tms.
        var prop = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(p => typeof(IMoveRule).IsAssignableFrom(p.PropertyType));
        if (prop != null)
        {
            try
            {
                var val = prop.GetValue(ruleSO);
                if (val is IMoveRule rule) return rule;
            }
            catch { /* ignore */ }
        }

        // Ei pystytty rakentamaan — palauta null (käytetään resolveria pelissä)
        return null;
    }
}
