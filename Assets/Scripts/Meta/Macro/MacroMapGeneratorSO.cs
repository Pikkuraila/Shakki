using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/MacroMap Generator", fileName = "MacroMapGenerator")]
public class MacroMapGeneratorSO : ScriptableObject
{
    [Header("Map Size")]
    [Min(1)] public int rows = 12;
    [Min(1)] public int columns = 3;

    [Header("Event Rules (Inspector tunable)")]
    public List<MacroEventRule> rules = new List<MacroEventRule>
    {
    new MacroEventRule { type = MacroEventType.Battle, chancePerRow = 0f, weightWhenFilling = 10, rollDifficultyOffset = true, difficultyOffsetRange = new Vector2Int(-1,1) },
    new MacroEventRule { type = MacroEventType.RandomEvent, chancePerRow = 0.20f, weightWhenFilling = 3 },
    new MacroEventRule { type = MacroEventType.Shop, chancePerRow = 0.15f, weightWhenFilling = 0, shopTierOffset = 0 },
    new MacroEventRule { type = MacroEventType.Rest, chancePerRow = 0.10f, weightWhenFilling = 0 },
    new MacroEventRule { type = MacroEventType.Alchemist, chancePerRow = 0.08f, weightWhenFilling = 0 },
    };

    [Header("Rules")]
    public bool guaranteeOneBattlePerRow = true;

    [Header("Boss Placement")]
    public bool generateBoss = true;
    public int bossRowOffset = -1;

    [System.Serializable]
    public struct MacroEventRule
    {
        public MacroEventType type;

        [Header("Row placement (0..1)")]
        [Range(0f, 1f)] public float chancePerRow;   // yrittää asettaa 1 kpl per rivi

        [Header("Fill weights")]
        [Min(0)] public int weightWhenFilling;       // kun täytetään tyhjät, käytä tätä painoa

        [Header("Optional offsets")]
        public bool rollDifficultyOffset;            // battle-tyyppisille
        public Vector2Int difficultyOffsetRange;     // esim (-1,1)
        public int shopTierOffset;                   // shop-tyyppisille
    }


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

        // 1) Yritä asettaa rulejen "chancePerRow" eventit (max 1 per rule per row)
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule.type == MacroEventType.None) continue;
            if (rule.chancePerRow <= 0f) continue;

            if (Random.value < rule.chancePerRow)
                TryPlaceSpecialInEmpty(map, row, rule.type);
        }

        // 2) Täytä loput painotetulla fillillä
        for (int c = 0; c < columns; c++)
        {
            int idx = map.GetIndex(row, c);
            if (map.tiles[idx].type != MacroEventType.None) continue;

            map.tiles[idx] = RollFillTile();
        }

        // 3) pakota vähintään yksi battle (niin kuin ennen)
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

        // 4) offsetit ruutuihin (säännöistä)
        for (int c = 0; c < columns; c++)
        {
            int idx = map.GetIndex(row, c);
            var t = map.tiles[idx];

            var rule = GetRule(t.type);
            if (rule.HasValue)
            {
                var r = rule.Value;
                if (t.type == MacroEventType.Battle && r.rollDifficultyOffset)
                {
                    int min = Mathf.Min(r.difficultyOffsetRange.x, r.difficultyOffsetRange.y);
                    int max = Mathf.Max(r.difficultyOffsetRange.x, r.difficultyOffsetRange.y);
                    t.difficultyOffset = Random.Range(min, max + 1);
                }

                if (t.type == MacroEventType.Shop)
                {
                    t.shopTierOffset = r.shopTierOffset;
                }
            }

            map.tiles[idx] = t;
        }
    }

    private MacroTileDef RollFillTile()
    {
        // kerää kaikki rule-tyypit joilla weightWhenFilling > 0
        int total = 0;
        for (int i = 0; i < rules.Count; i++)
            total += Mathf.Max(0, rules[i].weightWhenFilling);

        // fallback: jos kaikki weightit 0, laita RandomEvent
        if (total <= 0)
            return new MacroTileDef { type = MacroEventType.RandomEvent };

        int roll = Random.Range(0, total);
        for (int i = 0; i < rules.Count; i++)
        {
            int w = Mathf.Max(0, rules[i].weightWhenFilling);
            if (w == 0) continue;

            if (roll < w)
                return new MacroTileDef { type = rules[i].type };

            roll -= w;
        }

        return new MacroTileDef { type = MacroEventType.RandomEvent };
    }

    private MacroEventRule? GetRule(MacroEventType type)
    {
        for (int i = 0; i < rules.Count; i++)
            if (rules[i].type == type) return rules[i];
        return null;
    }

    private void TryPlaceSpecialInEmpty(MacroMapSO map, int row, MacroEventType type)
    {
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
