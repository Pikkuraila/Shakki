using UnityEngine;

public static class RunStatePersistence
{
    public static bool TryBuildSavedMacroMap(
        PlayerData data,
        RunController.MacroBuildMode buildMode,
        MacroMapSO macroPreset,
        MacroMapGeneratorSO macroGenerator,
        out MacroMapSO map)
    {
        map = null;

        if (data == null)
            return false;

        if (buildMode == RunController.MacroBuildMode.GenerateRandom)
        {
            if (string.IsNullOrEmpty(data.lastRunSeed) || macroGenerator == null)
                return false;

            if (!int.TryParse(data.lastRunSeed, out int seed))
                return false;

            map = macroGenerator.Generate(seed);
            return map != null;
        }

        if (buildMode == RunController.MacroBuildMode.UsePreset)
        {
            if (macroPreset == null || data.macroIndex <= 0)
                return false;

            map = CloneMacroMap(macroPreset);
            return map != null;
        }

        return false;
    }

    public static string FormatStoredRunSeed(RunController.MacroBuildMode buildMode, int? generatedSeed)
    {
        return buildMode == RunController.MacroBuildMode.GenerateRandom && generatedSeed.HasValue
            ? generatedSeed.Value.ToString()
            : null;
    }

    public static int GetRunStartIndex(MacroMapSO map)
    {
        if (map == null)
            return 0;

        int startRow = 0;
        int startCol = map.columns / 2;
        return map.GetIndex(startRow, startCol);
    }

    public static MacroMapSO CloneMacroMap(MacroMapSO src)
    {
        if (src == null)
            return null;

        var map = ScriptableObject.CreateInstance<MacroMapSO>();
        map.rows = Mathf.Max(1, src.rows);
        map.columns = Mathf.Max(1, src.columns);

        int tileCount = map.rows * map.columns;
        map.tiles = new MacroTileDef[tileCount];

        if (src.tiles != null)
        {
            int copyCount = Mathf.Min(src.tiles.Length, tileCount);
            for (int i = 0; i < copyCount; i++)
                map.tiles[i] = src.tiles[i];
        }

        return map;
    }
}
