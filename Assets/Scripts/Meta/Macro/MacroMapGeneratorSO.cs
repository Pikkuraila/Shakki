using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/MacroMap Generator", fileName = "MacroMapGenerator")]
public class MacroMapGeneratorSO : ScriptableObject
{
    [Header("Map Size")]
    [Min(1)] public int rows = 12;
    [Min(1)] public int columns = 3;

    [Header("Global Event Weights")]
    public int weightBattle = 10;
    public int weightShop = 2;
    public int weightRest = 1;
    public int weightRandom = 3;

    [Header("Rules")]
    public bool guaranteeOneBattlePerRow = true;
    public float shopChancePerRow = 0.15f;
    public float restChancePerRow = 0.10f;
    public float randomChancePerRow = 0.20f;

    [Header("Boss Placement")]
    public bool generateBoss = true;
    public int bossRowOffset = -1;

    // ✅ vanha kutsu toimii edelleen
    public MacroMapSO Generate()
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);
        return Generate(seed);
    }

    // ✅ tämä on se mitä RunController nyt kutsuu
    public MacroMapSO Generate(int seed)
    {
        var prev = Random.state;
        Random.InitState(seed);

        try
        {
            var map = ScriptableObject.CreateInstance<MacroMapSO>();
            map.rows = rows;
            map.columns = columns;
            map.tiles = new MacroTileDef[rows * columns];

            for (int r = 0; r < rows; r++)
                GenerateRow(map, r);

            // Start tile keskelle ylös (turvallinen)
            int startRow = 0;
            int startCol = columns / 2;
            map.tiles[map.GetIndex(startRow, startCol)] = new MacroTileDef { type = MacroEventType.None };

            // Boss viimeiselle riville keskelle
            if (generateBoss)
            {
                int br = Mathf.Clamp(rows + bossRowOffset, 0, rows - 1);
                int bc = columns / 2;
                map.tiles[map.GetIndex(br, bc)] = new MacroTileDef { type = MacroEventType.Boss };
            }

            return map;
        }
        finally
        {
            Random.state = prev;
        }
    }

    private void GenerateRow(MacroMapSO map, int row)
    {
        int columns = map.columns;

        // 0) nollaa rivi
        for (int c = 0; c < columns; c++)
            map.tiles[map.GetIndex(row, c)] = new MacroTileDef { type = MacroEventType.None };

        // 1) specialit tyhjiin sloteihin
        TryPlaceSpecialInEmpty(map, row, MacroEventType.Shop, shopChancePerRow);
        TryPlaceSpecialInEmpty(map, row, MacroEventType.Rest, restChancePerRow);
        TryPlaceSpecialInEmpty(map, row, MacroEventType.RandomEvent, randomChancePerRow);

        // 2) täytä loput perusjakaumalla
        for (int c = 0; c < columns; c++)
        {
            int idx = map.GetIndex(row, c);
            if (map.tiles[idx].type != MacroEventType.None) continue;

            map.tiles[idx] = RollBaseTile();
        }

        // 3) pakota vähintään yksi battle
        if (guaranteeOneBattlePerRow)
        {
            bool anyBattle = false;
            for (int c = 0; c < columns; c++)
                if (map.tiles[map.GetIndex(row, c)].type == MacroEventType.Battle) { anyBattle = true; break; }

            if (!anyBattle)
            {
                int chosen = map.GetIndex(row, Random.Range(0, columns));
                map.tiles[chosen] = new MacroTileDef { type = MacroEventType.Battle };
            }
        }

        // 4) (valinnainen) offsetit
        for (int c = 0; c < columns; c++)
        {
            int idx = map.GetIndex(row, c);
            var t = map.tiles[idx];

            if (t.type == MacroEventType.Battle)
                t.difficultyOffset = Random.Range(-1, 2); // -1..1

            if (t.type == MacroEventType.Shop)
                t.shopTierOffset = 0;

            map.tiles[idx] = t;
        }
    }

    private MacroTileDef RollBaseTile()
    {
        int total = weightBattle + weightRandom;
        int roll = Random.Range(0, total);
        if (roll < weightBattle) return new MacroTileDef { type = MacroEventType.Battle };
        return new MacroTileDef { type = MacroEventType.RandomEvent };
    }

    private void TryPlaceSpecialInEmpty(MacroMapSO map, int row, MacroEventType type, float chance)
    {
        if (Random.value >= chance) return;

        int columns = map.columns;
        var emptyCols = new List<int>();

        for (int c = 0; c < columns; c++)
        {
            int idx = map.GetIndex(row, c);
            if (map.tiles[idx].type == MacroEventType.None)
                emptyCols.Add(c);
        }

        if (emptyCols.Count == 0) return;

        int col = emptyCols[Random.Range(0, emptyCols.Count)];
        int i = map.GetIndex(row, col);
        map.tiles[i] = new MacroTileDef { type = type };
    }
}
