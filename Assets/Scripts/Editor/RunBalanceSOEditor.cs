using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RunBalanceSO))]
public sealed class RunBalanceSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var balance = (RunBalanceSO)target;
        var preview = balance.GetPreview();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Computed Preview", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            $"Row: {preview.row}\n" +
            $"Diff Offset: {preview.tileDifficultyOffset}\n" +
            $"Shop Offset: {preview.tileShopOffset}\n" +
            $"Boss: {(preview.isBoss ? "Yes" : "No")}\n\n" +
            $"Battle Tier: {preview.battleTier}\n" +
            $"Shop Tier: {preview.shopTier}\n" +
            $"Budget: {preview.budget}\n" +
            $"Cap Cost: {preview.capCost}",
            MessageType.Info
        );
    }
}