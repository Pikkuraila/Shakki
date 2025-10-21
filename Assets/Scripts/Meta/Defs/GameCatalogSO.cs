using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/GameCatalog", fileName = "GameCatalog")]
public sealed class GameCatalogSO : ScriptableObject
{
    public List<PieceDefSO> pieces;
    public List<UpgradeDefSO> upgrades;
    public List<PowerupDefSO> powerups; 

    public PieceDefSO GetPieceById(string id)
        => pieces.Find(p => p && (p.typeName == id || p.name == id));

    public UpgradeDefSO GetUpgradeById(string id)
        => upgrades.Find(u => u && u.id == id);
}
