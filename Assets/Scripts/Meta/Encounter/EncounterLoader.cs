using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Shakki.Core;

public static class EncounterLoader
{
    public static void Apply(GameState s, EncounterSO enc, GameCatalogSO catalog)
    {
        Debug.Log($"[EL] absolute-only board={s.Width}x{s.Height}");

        if (enc.spawns == null || enc.spawns.Count == 0)
        {
            Debug.LogWarning("[EL] No spawns provided.");
        }
        else
        {
            Debug.Log("[EL] spawns-in: " + string.Join(", ",
                enc.spawns.Select(sp =>
                    $"{sp.owner}:{sp.pieceId}@({sp.x},{sp.y})" +
                    (string.IsNullOrEmpty(sp.pieceInstanceId) ? "" : $"#{sp.pieceInstanceId}"))));

            var firstOwner = enc.spawns[0].owner;
            if (string.IsNullOrEmpty(firstOwner))
                Debug.LogWarning("[EL] First spawn owner is null/empty.");
        }

        if (s == null) { Debug.LogError("[EncounterLoader] GameState is null"); return; }
        if (enc == null) { Debug.LogError("[EncounterLoader] EncounterSO is null"); return; }
        if (catalog == null) { Debug.LogError("[EncounterLoader] GameCatalogSO is null"); return; }

        ClearBoardBySettingNulls(s);

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

            var c = new Coord(sp.x, sp.y);
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

            var piece = CreatePieceFromDef(sp.owner, def, sp.pieceInstanceId);
            if (piece == null)
            {
                Debug.LogError($"[EncounterLoader] Could not construct Piece for '{def?.typeName}'");
                continue;
            }

            s.Set(c, piece);
        }
    }

    private static void ClearBoardBySettingNulls(GameState s)
    {
        foreach (var c in s.AllCoords())
            s.Set(c, null);
    }

    private static Piece CreatePieceFromDef(string owner, PieceDefSO def, string pieceInstanceId = null)
    {
        var rules = TryBuildRules(def) ?? Array.Empty<IMoveRule>();
        var piece = new Piece(owner, def.typeName, rules, def.GetComputedTags(), pieceInstanceId)
        {
            HasMoved = false
        };
        return piece;
    }

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

    private static IMoveRule BuildRuleSafe(ScriptableObject ruleSO)
    {
        if (ruleSO == null) return null;

        var t = ruleSO.GetType();
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

        var prop = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(p => typeof(IMoveRule).IsAssignableFrom(p.PropertyType));
        if (prop != null)
        {
            try
            {
                var val = prop.GetValue(ruleSO);
                if (val is IMoveRule rule) return rule;
            }
            catch { }
        }

        return null;
    }
}
