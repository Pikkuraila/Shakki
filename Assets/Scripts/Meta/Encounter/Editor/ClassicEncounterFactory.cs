#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ClassicEncounterFactory
{
    [MenuItem("Shakki/Create Classic 8x8 Encounter")]
    public static void CreateClassic()
    {
        const string folder = "Assets/ScriptableObjects/Meta/Encounter";
        const string assetPath = folder + "/Classic_8x8.asset";
        System.IO.Directory.CreateDirectory(folder);

        var e = ScriptableObject.CreateInstance<EncounterSO>();
        e.name = "Classic_8x8";

        e.requireWhiteKing = true;
        e.requireBlackKing = true;

        e.minRecommendedTier = 1;
        e.maxRecommendedTier = 999;

        // Valkoinen back rank (y=0)
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Rook",   x = 0, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Knight", x = 1, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Bishop", x = 2, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Queen",  x = 3, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "King",   x = 4, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Bishop", x = 5, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Knight", x = 6, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Rook",   x = 7, y = 0 });

        // Valkoinen pawn row (y=1)
        for (int x = 0; x < 8; x++)
            e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Pawn", x = x, y = 1 });

        // Musta pawn row (y=6)
        for (int x = 0; x < 8; x++)
            e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Pawn", x = x, y = 6 });

        // Musta back rank (y=7)
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Rook",   x = 0, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Knight", x = 1, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Bishop", x = 2, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Queen",  x = 3, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "King",   x = 4, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Bishop", x = 5, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Knight", x = 6, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Rook",   x = 7, y = 7 });

        AssetDatabase.CreateAsset(e, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = e;

        Debug.Log($"[Shakki] Created Classic Encounter at {assetPath}");
    }
}
#endif