using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(EncounterSO))]
public sealed class EncounterSOEditor : Editor
{
    private const int GridSize = 8;
    private const float CellSize = 36f;

    private string selectedOwner = "black";
    private string selectedPieceId = "Pawn";
    private bool eraseMode = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var enc = (EncounterSO)target;
        if (enc == null)
            return;

        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Encounter Grid Editor", EditorStyles.boldLabel);

        DrawPalette();
        DrawTools(enc);
        DrawGrid(enc);

        EditorGUILayout.Space(12);
        DrawSummary(enc);
        DrawValidation(enc);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(enc);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPalette()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        selectedOwner = EditorGUILayout.Popup(
            "Owner",
            selectedOwner == "white" ? 0 : 1,
            new[] { "white", "black" }
        ) == 0 ? "white" : "black";

        selectedPieceId = EditorGUILayout.TextField("Piece Id", selectedPieceId);
        eraseMode = EditorGUILayout.Toggle("Erase Mode", eraseMode);

        EditorGUILayout.HelpBox(
            eraseMode
                ? "Click a cell to remove a spawn."
                : $"Click a cell to place: {selectedOwner}:{selectedPieceId}",
            MessageType.Info
        );
    }

    private void DrawTools(EncounterSO enc)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear Board", GUILayout.Height(28)))
        {
            Undo.RecordObject(enc, "Clear Encounter Board");
            enc.spawns.Clear();
            EditorUtility.SetDirty(enc);
        }

        if (GUILayout.Button("Sort Spawns", GUILayout.Height(28)))
        {
            Undo.RecordObject(enc, "Sort Encounter Spawns");
            enc.spawns.Sort((a, b) =>
            {
                int oy = a.y.CompareTo(b.y);
                if (oy != 0) return oy;

                int ox = a.x.CompareTo(b.x);
                if (ox != 0) return ox;

                int oo = string.Compare(a.owner, b.owner, System.StringComparison.Ordinal);
                if (oo != 0) return oo;

                return string.Compare(a.pieceId, b.pieceId, System.StringComparison.Ordinal);
            });
            EditorUtility.SetDirty(enc);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGrid(EncounterSO enc)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Board", EditorStyles.boldLabel);

        for (int y = GridSize - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(y.ToString(), GUILayout.Width(18));

            for (int x = 0; x < GridSize; x++)
            {
                int spawnIndex = FindSpawnIndex(enc, x, y);
                bool hasSpawn = spawnIndex >= 0;

                string label = ".";
                string tooltip = $"({x},{y})";

                if (hasSpawn)
                {
                    var sp = enc.spawns[spawnIndex];
                    label = GetSpawnLabel(sp);
                    tooltip = $"{sp.owner}:{sp.pieceId} @ ({x},{y})";
                }

                var content = new GUIContent(label, tooltip);

                if (GUILayout.Button(content, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                {
                    HandleCellClick(enc, x, y, spawnIndex);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        for (int x = 0; x < GridSize; x++)
            GUILayout.Label(x.ToString(), GUILayout.Width(CellSize));
        EditorGUILayout.EndHorizontal();
    }

    private void HandleCellClick(EncounterSO enc, int x, int y, int existingIndex)
    {
        Undo.RecordObject(enc, "Edit Encounter Spawn");

        if (eraseMode)
        {
            if (existingIndex >= 0)
            {
                enc.spawns.RemoveAt(existingIndex);
                EditorUtility.SetDirty(enc);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedPieceId))
            return;

        var newSpawn = new EncounterSO.Spawn
        {
            owner = selectedOwner,
            pieceId = selectedPieceId.Trim(),
            x = x,
            y = y
        };

        if (existingIndex >= 0)
            enc.spawns[existingIndex] = newSpawn;
        else
            enc.spawns.Add(newSpawn);

        EditorUtility.SetDirty(enc);
    }

    private int FindSpawnIndex(EncounterSO enc, int x, int y)
    {
        if (enc.spawns == null)
            return -1;

        for (int i = 0; i < enc.spawns.Count; i++)
        {
            var sp = enc.spawns[i];
            if (sp.x == x && sp.y == y)
                return i;
        }

        return -1;
    }

    private string GetSpawnLabel(EncounterSO.Spawn sp)
    {
        if (string.IsNullOrWhiteSpace(sp.pieceId))
            return "?";

        string ownerPrefix = string.IsNullOrWhiteSpace(sp.owner)
            ? "?"
            : sp.owner.Trim().ToLowerInvariant() == "white" ? "W" : "B";

        string piece = sp.pieceId.Trim();
        string pieceShort = piece.Length <= 2 ? piece : piece.Substring(0, 2);

        return ownerPrefix + pieceShort;
    }

    private void DrawSummary(EncounterSO enc)
    {
        int spawnCount = 0;
        int whiteCount = 0;
        int blackCount = 0;
        int whiteKings = 0;
        int blackKings = 0;
        int invalidEntries = 0;

        if (enc.spawns != null)
        {
            for (int i = 0; i < enc.spawns.Count; i++)
            {
                var sp = enc.spawns[i];

                bool validOwner = !string.IsNullOrWhiteSpace(sp.owner);
                bool validPiece = !string.IsNullOrWhiteSpace(sp.pieceId);

                if (!validOwner || !validPiece)
                {
                    invalidEntries++;
                    continue;
                }

                spawnCount++;

                string owner = sp.owner.Trim().ToLowerInvariant();
                string piece = sp.pieceId.Trim();

                if (owner == "white")
                {
                    whiteCount++;
                    if (piece == "King") whiteKings++;
                }
                else if (owner == "black")
                {
                    blackCount++;
                    if (piece == "King") blackKings++;
                }
            }
        }

        GUILayout.Label("Encounter Summary", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            $"Tier Range: {enc.minRecommendedTier} - {enc.maxRecommendedTier}\n" +
            $"Spawn Count: {spawnCount}\n" +
            $"White Pieces: {whiteCount} (Kings: {whiteKings})\n" +
            $"Black Pieces: {blackCount} (Kings: {blackKings})\n" +
            $"Invalid Entries: {invalidEntries}",
            MessageType.Info
        );
    }

    private void DrawValidation(EncounterSO enc)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        bool anyWarnings = false;

        if (enc.spawns == null || enc.spawns.Count == 0)
        {
            EditorGUILayout.HelpBox("Encounter has no spawns.", MessageType.Warning);
            anyWarnings = true;
        }

        var seen = new HashSet<Vector2Int>();
        int duplicateCoords = 0;
        int invalidOwnerCount = 0;
        int emptyPieceCount = 0;
        int whiteKings = 0;
        int blackKings = 0;

        if (enc.spawns != null)
        {
            for (int i = 0; i < enc.spawns.Count; i++)
            {
                var sp = enc.spawns[i];

                var pos = new Vector2Int(sp.x, sp.y);
                if (!seen.Add(pos))
                    duplicateCoords++;

                if (string.IsNullOrWhiteSpace(sp.owner) ||
                    (sp.owner.Trim().ToLowerInvariant() != "white" &&
                     sp.owner.Trim().ToLowerInvariant() != "black"))
                {
                    invalidOwnerCount++;
                }

                if (string.IsNullOrWhiteSpace(sp.pieceId))
                    emptyPieceCount++;

                if (!string.IsNullOrWhiteSpace(sp.owner) && !string.IsNullOrWhiteSpace(sp.pieceId))
                {
                    string owner = sp.owner.Trim().ToLowerInvariant();
                    string piece = sp.pieceId.Trim();

                    if (piece == "King")
                    {
                        if (owner == "white") whiteKings++;
                        if (owner == "black") blackKings++;
                    }
                }

                if (sp.x < 0 || sp.x >= GridSize || sp.y < 0 || sp.y >= GridSize)
                {
                    EditorGUILayout.HelpBox($"Spawn out of editor grid bounds at ({sp.x},{sp.y}).", MessageType.Warning);
                    anyWarnings = true;
                }
            }
        }

        if (duplicateCoords > 0)
        {
            EditorGUILayout.HelpBox($"Encounter contains {duplicateCoords} duplicate coordinate entries.", MessageType.Warning);
            anyWarnings = true;
        }

        if (invalidOwnerCount > 0)
        {
            EditorGUILayout.HelpBox($"Encounter contains {invalidOwnerCount} invalid owner value(s). Use only 'white' or 'black'.", MessageType.Warning);
            anyWarnings = true;
        }

        if (emptyPieceCount > 0)
        {
            EditorGUILayout.HelpBox($"Encounter contains {emptyPieceCount} empty pieceId value(s).", MessageType.Warning);
            anyWarnings = true;
        }

        if (enc.requireWhiteKing && whiteKings == 0)
        {
            EditorGUILayout.HelpBox("requireWhiteKing is enabled, but no white King spawn was found.", MessageType.Warning);
            anyWarnings = true;
        }

        if (enc.requireBlackKing && blackKings == 0)
        {
            EditorGUILayout.HelpBox("requireBlackKing is enabled, but no black King spawn was found.", MessageType.Warning);
            anyWarnings = true;
        }

        if (!anyWarnings)
        {
            EditorGUILayout.HelpBox("No obvious issues detected.", MessageType.None);
        }
    }
}