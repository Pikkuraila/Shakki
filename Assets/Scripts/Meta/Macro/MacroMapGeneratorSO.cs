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
    public float shopChancePerRow = 0.15f;       // keskimäärin yksi shop ~6–7 rivissä
    public float restChancePerRow = 0.10f;
    public float randomChancePerRow = 0.20f;

    [Header("Boss Placement")]
    public bool generateBoss = true;
    public int bossRowOffset = -1;   // viimeiselle riville (rows - 1)

    /// <summary>
    /// Generoi uuden MacroMapSO-instanssin sääntöjen mukaan.
    /// </summary>
    public MacroMapSO Generate()
    {
        var map = ScriptableObject.CreateInstance<MacroMapSO>();
        map.rows = rows;
        map.columns = columns;
        map.tiles = new MacroTileDef[rows * columns];

        for (int r = 0; r < rows; r++)
        {
            GenerateRow(map, r);
        }

        // Boss sijoitetaan
        if (generateBoss)
        {
            int br = Mathf.Clamp(rows + bossRowOffset, 0, rows - 1);
            int bc = columns / 2;
            map.tiles[map.GetIndex(br, bc)] = new MacroTileDef
            {
                type = MacroEventType.Boss
            };
        }

        return map;
    }

    private void GenerateRow(MacroMapSO map, int row)
    {
        int columns = map.columns;

        // Heuristiikka: perusriville arvotaan tile-tyypit
        for (int c = 0; c < columns; c++)
        {
            int idx = map.GetIndex(row, c);
            map.tiles[idx] = RollTile();
        }

        // Pakotetaan vähintään 1 battle jos halutaan
        if (guaranteeOneBattlePerRow)
        {
            bool anyBattle = false;
            for (int c = 0; c < columns; c++)
            {
                if (map.tiles[map.GetIndex(row, c)].type == MacroEventType.Battle)
                {
                    anyBattle = true;
                    break;
                }
            }

            if (!anyBattle)
            {
                int c = Random.Range(0, columns);
                int idx = map.GetIndex(row, c);
                map.tiles[idx].type = MacroEventType.Battle;
            }
        }

        // Chance-lisäykset (shop/rest/random)
        TryPlaceSpecial(map, row, MacroEventType.Shop, shopChancePerRow);
        TryPlaceSpecial(map, row, MacroEventType.Rest, restChancePerRow);
        TryPlaceSpecial(map, row, MacroEventType.RandomEvent, randomChancePerRow);
    }

    private MacroTileDef RollTile()
    {
        int total =
            weightBattle +
            weightShop +
            weightRest +
            weightRandom;

        int roll = Random.Range(0, total);

        if (roll < weightBattle) return new MacroTileDef { type = MacroEventType.Battle };
        roll -= weightBattle;

        if (roll < weightShop) return new MacroTileDef { type = MacroEventType.Shop };
        roll -= weightShop;

        if (roll < weightRest) return new MacroTileDef { type = MacroEventType.Rest };
        roll -= weightRest;

        return new MacroTileDef { type = MacroEventType.RandomEvent };
    }

    private void TryPlaceSpecial(MacroMapSO map, int row, MacroEventType type, float chance)
    {
        if (Random.value >= chance) return;

        int columns = map.columns;
        int col = Random.Range(0, columns);
        int idx = map.GetIndex(row, col);

        map.tiles[idx] = new MacroTileDef { type = type };
    }
}
