using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Run/Run Balance", fileName = "RunBalance")]
public sealed class RunBalanceSO : ScriptableObject
{
    [Header("Run Progression")]
    [Min(1)] public int baseBattleDifficulty = 1;
    [Min(0)] public int battleDifficultyPerRow = 1;

    [Min(1)] public int baseShopTier = 1;
    [Min(0)] public int shopTierPerRow = 1;

    [Min(0)] public int bossDifficultyBonus = 1;

    [Header("Procedural Battle Budget")]
    [Min(0)] public int budgetBase = 2;
    [Min(0)] public int budgetPerTier = 2;

    [Min(1)] public int capCostBase = 1;
    [Min(0)] public int capCostPerTier = 1;

    [Min(0.1f)] public float cheapPieceBiasPower = 1.2f;

    [Header("Special Rules")]
    [Min(1)] public int forceBlackKingThreshold = 5;

    [Header("Debug Preview")]
    [Min(0)] public int previewRow = 0;
    public int previewTileDifficultyOffset = 0;
    public int previewTileShopOffset = 0;
    public bool previewAsBoss = false;

    public int GetBattleDifficulty(int row, int tileDifficultyOffset = 0, bool isBoss = false)
    {
        int value =
            baseBattleDifficulty +
            row * battleDifficultyPerRow +
            tileDifficultyOffset;

        if (isBoss)
            value += bossDifficultyBonus;

        return Mathf.Max(1, value);
    }

    public int GetShopTier(int row, int tileShopOffset = 0)
    {
        int value =
            baseShopTier +
            row * shopTierPerRow +
            tileShopOffset;

        return Mathf.Max(1, value);
    }

    public int GetBudgetForTier(int tier)
    {
        return Mathf.Max(0, budgetBase + tier * budgetPerTier);
    }

    public int GetCapCostForTier(int tier)
    {
        return Mathf.Max(1, capCostBase + tier * capCostPerTier);
    }

    public BattleTuning GetBattleTuning(int row, int tileDifficultyOffset = 0, bool isBoss = false)
    {
        int tier = GetBattleDifficulty(row, tileDifficultyOffset, isBoss);

        return new BattleTuning
        {
            tier = tier,
            budget = GetBudgetForTier(tier),
            capCost = GetCapCostForTier(tier),
            cheapPieceBiasPower = cheapPieceBiasPower,
            forceBlackKingThreshold = forceBlackKingThreshold
        };
    }

    public PreviewSnapshot GetPreview()
    {
        int battleTier = GetBattleDifficulty(previewRow, previewTileDifficultyOffset, previewAsBoss);
        int shopTier = GetShopTier(previewRow, previewTileShopOffset);
        var tuning = GetBattleTuning(previewRow, previewTileDifficultyOffset, previewAsBoss);

        return new PreviewSnapshot
        {
            row = previewRow,
            tileDifficultyOffset = previewTileDifficultyOffset,
            tileShopOffset = previewTileShopOffset,
            isBoss = previewAsBoss,
            battleTier = battleTier,
            shopTier = shopTier,
            budget = tuning.budget,
            capCost = tuning.capCost
        };
    }

    [System.Serializable]
    public struct BattleTuning
    {
        public int tier;
        public int budget;
        public int capCost;
        public float cheapPieceBiasPower;
        public int forceBlackKingThreshold;
    }

    [System.Serializable]
    public struct PreviewSnapshot
    {
        public int row;
        public int tileDifficultyOffset;
        public int tileShopOffset;
        public bool isBoss;
        public int battleTier;
        public int shopTier;
        public int budget;
        public int capCost;
    }
}