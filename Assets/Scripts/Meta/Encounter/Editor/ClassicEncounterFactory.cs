#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ClassicEncounterFactory
{
    [MenuItem("Shakki/Create Classic 8×8 Encounter")]
    public static void CreateClassic()
    {
        // Mihin talletetaan asset
        const string folder = "Assets/ScriptableObjects/Meta/Encounter";
        const string assetPath = folder + "/Classic_8x8.asset";
        System.IO.Directory.CreateDirectory(folder);

        // Luo EncounterSO
        var e = ScriptableObject.CreateInstance<EncounterSO>();
        e.name = "Classic_8x8";

        // Käytetään absoluuttisia koordinaatteja (0..7)
        e.relativeRanks = false;

        // Sotilaat
        e.fillWhitePawnsAtY = true; e.whitePawnsY = 1;
        e.fillBlackPawnsAtY = true; e.blackPawnsY = 6;

        // Valkoisen takarivi (y=0)
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Rook",   x=0, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Knight", x=1, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Bishop", x=2, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Queen",  x=3, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="King",   x=4, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Bishop", x=5, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Knight", x=6, y=0 });
        e.spawns.Add(new EncounterSO.Spawn { owner="white", pieceId="Rook",   x=7, y=0 });

        // Mustan takarivi (y=7)
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Rook",   x=0, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Knight", x=1, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Bishop", x=2, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Queen",  x=3, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="King",   x=4, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Bishop", x=5, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Knight", x=6, y=7 });
        e.spawns.Add(new EncounterSO.Spawn { owner="black", pieceId="Rook",   x=7, y=7 });

        // Tallenna asset
        AssetDatabase.CreateAsset(e, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = e;

        Debug.Log($"[Shakki] Created Classic Encounter at {assetPath}");
    }
}
#endif
