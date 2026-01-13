using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/Encounter Pool", fileName = "EncounterPool")]
public sealed class EncounterPoolSO : ScriptableObject
{
    public List<TierPool> tiers = new();

    public EncounterSO Pick(int difficultyTier)
    {
        if (tiers == null || tiers.Count == 0)
            return null;

        TierPool best = null;
        int bestDist = int.MaxValue;

        foreach (var t in tiers)
        {
            int d = Mathf.Abs(t.tier - difficultyTier);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }

        return best != null ? best.PickWeighted() : null;
    }

    [Serializable]
    public sealed class TierPool
    {
        public int tier;
        public List<WeightedEncounter> encounters = new();

        public EncounterSO PickWeighted()
        {
            if (encounters == null || encounters.Count == 0)
                return null;

            int total = 0;
            foreach (var e in encounters)
                total += Mathf.Max(0, e.weight);

            if (total <= 0)
                return encounters[0].encounter;

            int roll = UnityEngine.Random.Range(0, total);
            foreach (var e in encounters)
            {
                int w = Mathf.Max(0, e.weight);
                if (roll < w)
                    return e.encounter;
                roll -= w;
            }

            return encounters[0].encounter;
        }
    }

    [Serializable]
    public struct WeightedEncounter
    {
        public EncounterSO encounter;
        public int weight;
    }
}
