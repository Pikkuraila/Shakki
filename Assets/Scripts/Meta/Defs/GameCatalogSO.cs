using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/GameCatalog", fileName = "GameCatalog")]
public sealed class GameCatalogSO : ScriptableObject
{
    public List<PieceDefSO> pieces;
    public List<UpgradeDefSO> upgrades;
    public List<PowerupDefSO> powerups;

    // --- Runtime pieces (alchemy etc.) ---
    readonly Dictionary<string, PieceDefSO> _runtimePieces = new();

    public void RegisterRuntimePiece(PieceDefSO def)
    {
        if (def == null) return;

        // Valitse sun “yksi totuus” id:lle:
        var id = !string.IsNullOrEmpty(def.typeName) ? def.typeName : def.name;

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[GameCatalog] RegisterRuntimePiece: missing id/typeName");
            return;
        }

        _runtimePieces[id] = def;
        // Debug.Log($"[GameCatalog] Registered runtime piece '{id}'");
    }

    public void ClearRuntimePieces()
    {
        _runtimePieces.Clear();
    }

    public PieceDefSO GetPieceById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        // 1) runtime override first
        if (_runtimePieces.TryGetValue(id, out var rt) && rt != null)
            return rt;

        // 2) then static catalog
        return pieces.Find(p => p && (p.typeName == id || p.name == id));
    }

    public UpgradeDefSO GetUpgradeById(string id)
        => upgrades.Find(u => u && u.id == id);

    public PowerupDefSO GetPowerupById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var pu in powerups)
            if (pu != null && pu.id == id)
                return pu;
        return null;
    }
}
