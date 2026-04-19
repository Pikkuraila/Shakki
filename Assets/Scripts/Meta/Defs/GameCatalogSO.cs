using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(menuName = "Shakki/GameCatalog", fileName = "GameCatalog")]
public sealed class GameCatalogSO : ScriptableObject
{
    public List<PieceDefSO> pieces;
    public List<UpgradeDefSO> upgrades;
    public List<PowerupDefSO> powerups;
    public PieceDefSO amalgamBaseDef;

    // Runtime pieces created during the run, such as alchemy outputs.
    readonly Dictionary<string, PieceDefSO> _runtimePieces = new();

    public void RegisterRuntimePiece(PieceDefSO def)
    {
        if (def == null)
            return;

        var id = !string.IsNullOrEmpty(def.typeName) ? def.typeName : def.name;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[GameCatalog] RegisterRuntimePiece: missing id/typeName");
            return;
        }

        _runtimePieces[id] = def;
    }

    public void ClearRuntimePieces()
    {
        _runtimePieces.Clear();
    }

    public PieceDefSO GetPieceById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (_runtimePieces.TryGetValue(id, out var runtime) && runtime != null)
            return runtime;

        if (TryBuildRuntimeAmalgam(id, out var amalgam))
        {
            RegisterRuntimePiece(amalgam);
            return amalgam;
        }

        return FindPieceDef(id);
    }

    public UpgradeDefSO GetUpgradeById(string id)
        => upgrades.Find(u => u && u.id == id);

    public PowerupDefSO GetPowerupById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (var powerup in powerups)
        {
            if (powerup != null && powerup.id == id)
                return powerup;
        }

        return null;
    }

    private bool TryBuildRuntimeAmalgam(string id, out PieceDefSO runtime)
    {
        runtime = null;
        if (string.IsNullOrEmpty(id) || !id.StartsWith("Amalgam_", System.StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = id.Split(new[] { '_' }, 4);
        if (parts.Length < 4)
        {
            Debug.LogWarning($"[GameCatalog] Runtime amalgam id malformed: {id}");
            return false;
        }

        var baseDef = FindBaseAmalgamDef();
        var sourceA = FindPieceDef(parts[1]);
        var sourceB = FindPieceDef(parts[2]);
        if (baseDef == null || sourceA == null || sourceB == null)
        {
            Debug.LogWarning(
                $"[GameCatalog] Failed to rehydrate runtime amalgam '{id}' " +
                $"base={(baseDef != null ? baseDef.name : "NULL")} " +
                $"sourceA={parts[1]}:{(sourceA != null ? "OK" : "NULL")} " +
                $"sourceB={parts[2]}:{(sourceB != null ? "OK" : "NULL")}");
            return false;
        }

        runtime = Instantiate(baseDef);
        runtime.name = id;
        runtime.typeName = id;
        runtime.identityTags = IdentityTag.Amalgam | IdentityTag.Living;

        var baseRules = baseDef.rules ?? System.Array.Empty<MoveRuleSO>();
        var rulesA = sourceA.rules ?? System.Array.Empty<MoveRuleSO>();
        var rulesB = sourceB.rules ?? System.Array.Empty<MoveRuleSO>();

        runtime.rules = baseRules
            .Concat(rulesA)
            .Concat(rulesB)
            .Where(rule => rule != null)
            .Distinct()
            .ToArray();

        runtime.whiteSprite = baseDef.whiteSprite;
        runtime.blackSprite = baseDef.blackSprite;
        runtime.portraitSprite = baseDef.portraitSprite;
        runtime.displayName = baseDef.displayName;
        runtime.loreText = baseDef.loreText;
        runtime.cost = baseDef.cost;
        return true;
    }

    private PieceDefSO FindBaseAmalgamDef()
    {
        var fromCatalog = pieces?.Find(p =>
            p != null &&
            (string.Equals(p.typeName, "amalgam", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p.name, "Amalgam", System.StringComparison.OrdinalIgnoreCase)));

        if (fromCatalog != null)
            return fromCatalog;

        if (amalgamBaseDef != null)
            return amalgamBaseDef;

        return null;
    }

    private PieceDefSO FindPieceDef(string id)
    {
        if (string.IsNullOrEmpty(id) || pieces == null)
            return null;

        return pieces.Find(p =>
            p != null &&
            (string.Equals(p.typeName, id, System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p.name, id, System.StringComparison.OrdinalIgnoreCase)));
    }
}

