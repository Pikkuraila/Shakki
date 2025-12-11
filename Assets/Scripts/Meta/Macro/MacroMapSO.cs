using UnityEngine;

public enum MacroEventType
{
    None,
    Battle,
    Shop,
    Rest,
    RandomEvent,
    Boss
}

[System.Serializable]
public struct MacroTileDef
{
    public MacroEventType type;
    public string param; // esim. "Level1", "Hard", "GoldChest" tms.

    [Header("Scaling")]
    public int difficultyOffset; // käytetään Battle-tileille
    public int shopTierOffset;   // käytetään Shop-tileille
}

[CreateAssetMenu(menuName = "Shakki/Meta/MacroMap", fileName = "MacroMap")]
public sealed class MacroMapSO : ScriptableObject
{
    [Min(1)]
    public int rows = 1;

    [Min(1)]
    public int columns = 16;

    // lineaarinen lista ruutuja, pituus = rows * columns
    public MacroTileDef[] tiles;

    public int TileCount => tiles != null ? tiles.Length : 0;

    /// <summary>
    /// Palauttaa ruudun lineaarisella indeksillä (0..TileCount-1).
    /// </summary>
    public MacroTileDef GetTile(int index)
    {
        if (tiles == null || index < 0 || index >= tiles.Length)
        {
            Debug.LogError($"[MacroMapSO] GetTile index {index} out of range.");
            return default;
        }

        return tiles[index];
    }

    /// <summary>
    /// Muuntaa (row, column) → lineaariseksi indeksiksi.
    /// </summary>
    public int GetIndex(int row, int column)
    {
        return row * columns + column;
    }

    /// <summary>
    /// Palauttaa ruudun rivin ja sarakkeen perusteella.
    /// </summary>
    public MacroTileDef GetTile(int row, int column)
    {
        int index = GetIndex(row, column);
        return GetTile(index);
    }

    private void OnValidate()
    {
        if (rows < 1) rows = 1;
        if (columns < 1) columns = 1;

        int required = rows * columns;

        if (tiles == null || tiles.Length != required)
        {
            var newTiles = new MacroTileDef[required];

            // kopioi vanhat arvot niin pitkälle kuin mahtuu
            if (tiles != null)
            {
                int copyCount = Mathf.Min(tiles.Length, required);
                for (int i = 0; i < copyCount; i++)
                    newTiles[i] = tiles[i];
            }

            tiles = newTiles;
        }
    }
}
