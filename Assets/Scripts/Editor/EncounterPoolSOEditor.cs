using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EncounterPoolSO))]
public sealed class EncounterPoolSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var pool = (EncounterPoolSO)target;
        if (pool == null)
            return;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Pool Summary", EditorStyles.boldLabel);

        int tierCount = 0;
        int encounterCount = 0;
        int nullEncounterRefs = 0;
        int zeroOrNegativeWeights = 0;
        int mismatchedTierRanges = 0;
        int emptyTiers = 0;

        if (pool.tiers != null)
        {
            for (int i = 0; i < pool.tiers.Count; i++)
            {
                var tierPool = pool.tiers[i];
                if (tierPool == null)
                    continue;

                tierCount++;

                int localValidCount = 0;

                if (tierPool.encounters == null || tierPool.encounters.Count == 0)
                {
                    emptyTiers++;
                    continue;
                }

                for (int j = 0; j < tierPool.encounters.Count; j++)
                {
                    var entry = tierPool.encounters[j];

                    if (entry.encounter == null)
                    {
                        nullEncounterRefs++;
                        continue;
                    }

                    encounterCount++;
                    localValidCount++;

                    if (entry.weight <= 0)
                        zeroOrNegativeWeights++;

                    if (!entry.encounter.MatchesTier(tierPool.tier))
                        mismatchedTierRanges++;
                }

                if (localValidCount == 0)
                    emptyTiers++;
            }
        }

        EditorGUILayout.HelpBox(
            $"Tier Pools: {tierCount}\n" +
            $"Valid Encounters: {encounterCount}\n" +
            $"Null Encounter References: {nullEncounterRefs}\n" +
            $"Zero/Negative Weights: {zeroOrNegativeWeights}\n" +
            $"Tier/Range Mismatches: {mismatchedTierRanges}\n" +
            $"Empty Tiers: {emptyTiers}",
            MessageType.Info
        );

        DrawTierBreakdown(pool);
        DrawValidation(pool, nullEncounterRefs, zeroOrNegativeWeights, mismatchedTierRanges, emptyTiers);
    }

    private static void DrawTierBreakdown(EncounterPoolSO pool)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Tier Breakdown", EditorStyles.boldLabel);

        if (pool.tiers == null || pool.tiers.Count == 0)
        {
            EditorGUILayout.HelpBox("No tiers configured.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < pool.tiers.Count; i++)
        {
            var tierPool = pool.tiers[i];
            if (tierPool == null)
                continue;

            int validCount = 0;
            int nullCount = 0;
            int totalWeight = 0;
            int mismatchCount = 0;

            if (tierPool.encounters != null)
            {
                for (int j = 0; j < tierPool.encounters.Count; j++)
                {
                    var entry = tierPool.encounters[j];
                    if (entry.encounter == null)
                    {
                        nullCount++;
                        continue;
                    }

                    validCount++;

                    totalWeight += Mathf.Max(0, entry.weight);

                    if (!entry.encounter.MatchesTier(tierPool.tier))
                        mismatchCount++;
                }
            }

            string label =
                $"Tier {tierPool.tier}  |  " +
                $"Encounters: {validCount}  |  " +
                $"Nulls: {nullCount}  |  " +
                $"Total Weight: {totalWeight}  |  " +
                $"Range Mismatches: {mismatchCount}";

            MessageType msgType = mismatchCount > 0 || validCount == 0
                ? MessageType.Warning
                : MessageType.None;

            EditorGUILayout.HelpBox(label, msgType);
        }
    }

    private static void DrawValidation(
        EncounterPoolSO pool,
        int nullEncounterRefs,
        int zeroOrNegativeWeights,
        int mismatchedTierRanges,
        int emptyTiers)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        bool anyWarnings = false;

        if (pool.tiers == null || pool.tiers.Count == 0)
        {
            EditorGUILayout.HelpBox("Pool has no tier definitions.", MessageType.Warning);
            anyWarnings = true;
        }

        if (nullEncounterRefs > 0)
        {
            EditorGUILayout.HelpBox(
                $"Pool contains {nullEncounterRefs} null encounter reference{(nullEncounterRefs == 1 ? "" : "s")}.",
                MessageType.Warning
            );
            anyWarnings = true;
        }

        if (zeroOrNegativeWeights > 0)
        {
            EditorGUILayout.HelpBox(
                $"{zeroOrNegativeWeights} encounter entr{(zeroOrNegativeWeights == 1 ? "y has" : "ies have")} weight <= 0. Those entries may never be chosen unless everything else also resolves to zero.",
                MessageType.Warning
            );
            anyWarnings = true;
        }

        if (mismatchedTierRanges > 0)
        {
            EditorGUILayout.HelpBox(
                $"{mismatchedTierRanges} encounter entr{(mismatchedTierRanges == 1 ? "y does" : "ies do")} not match the enclosing pool tier based on min/max recommended tier.",
                MessageType.Warning
            );
            anyWarnings = true;
        }

        if (emptyTiers > 0)
        {
            EditorGUILayout.HelpBox(
                $"{emptyTiers} tier pool{(emptyTiers == 1 ? " is" : "s are")} empty or contain only null encounters.",
                MessageType.Warning
            );
            anyWarnings = true;
        }

        if (!HasDuplicateTierNumbers(pool))
        {
            if (!anyWarnings)
                EditorGUILayout.HelpBox("No obvious issues detected.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Pool contains duplicate tier numbers. This may be intentional, but it can make authoring and debugging harder.",
                MessageType.Warning
            );
        }
    }

    private static bool HasDuplicateTierNumbers(EncounterPoolSO pool)
    {
        if (pool == null || pool.tiers == null || pool.tiers.Count == 0)
            return false;

        for (int i = 0; i < pool.tiers.Count; i++)
        {
            var a = pool.tiers[i];
            if (a == null) continue;

            for (int j = i + 1; j < pool.tiers.Count; j++)
            {
                var b = pool.tiers[j];
                if (b == null) continue;

                if (a.tier == b.tier)
                    return true;
            }
        }

        return false;
    }
}