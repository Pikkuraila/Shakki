using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/Encounter Pool", fileName = "EncounterPool")]
public sealed class EncounterPoolSO : ScriptableObject
{
    public bool enableDebugLogs = true;
    public List<TierPool> tiers = new();

    public EncounterSO Pick(int difficultyTier)
    {
        if (tiers == null || tiers.Count == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[EncounterPool:{name}] Pick({difficultyTier}) failed: no tiers configured.");
            return null;
        }

        difficultyTier = Mathf.Max(1, difficultyTier);

        // 1) Exact tier pool ensin
        TierPool exactPool = null;
        for (int i = 0; i < tiers.Count; i++)
        {
            var t = tiers[i];
            if (t == null || !t.HasAnyValidEncounter())
                continue;

            if (t.tier == difficultyTier)
            {
                exactPool = t;
                break;
            }
        }

        if (exactPool != null)
        {
            var exactMatch = exactPool.PickWeightedForRequestedTier(difficultyTier);
            if (exactMatch != null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[EncounterPool:{name}] requestedTier={difficultyTier} -> exact tier={exactPool.tier} picked={exactMatch.name} " +
          $"encTierRange=[{exactMatch.minRecommendedTier}-{exactMatch.maxRecommendedTier}] " +
          $"spawnCount={(exactMatch.spawns != null ? exactMatch.spawns.Count : 0)}");
                return exactMatch;
            }
        }

        // 2) In-range match muista pooleista
        TierPool rangedPool = null;
        int rangedDist = int.MaxValue;

        for (int i = 0; i < tiers.Count; i++)
        {
            var t = tiers[i];
            if (t == null || !t.HasAnyValidEncounter())
                continue;

            if (t.ContainsEncounterMatchingTier(difficultyTier))
            {
                int dist = Mathf.Abs(t.tier - difficultyTier);
                if (dist < rangedDist)
                {
                    rangedDist = dist;
                    rangedPool = t;
                }
            }
        }

        if (rangedPool != null)
        {
            var rangedMatch = rangedPool.PickWeightedForRequestedTier(difficultyTier);
            if (rangedMatch != null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[EncounterPool:{name}] requestedTier={difficultyTier} -> ranged tier={rangedPool.tier} picked={rangedMatch.name}");
                return rangedMatch;
            }
        }

        // 3) Lähin alempi pool
        TierPool lowerPool = null;
        int bestLowerTier = int.MinValue;

        for (int i = 0; i < tiers.Count; i++)
        {
            var t = tiers[i];
            if (t == null || !t.HasAnyValidEncounter())
                continue;

            if (t.tier < difficultyTier && t.tier > bestLowerTier)
            {
                bestLowerTier = t.tier;
                lowerPool = t;
            }
        }

        if (lowerPool != null)
        {
            var lowerMatch = lowerPool.PickWeightedAny();
            if (lowerMatch != null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[EncounterPool:{name}] requestedTier={difficultyTier} -> fallback lower tier={lowerPool.tier} picked={lowerMatch.name}");
                return lowerMatch;
            }
        }

        // 4) Lähin ylempi pool
        TierPool higherPool = null;
        int bestHigherTier = int.MaxValue;

        for (int i = 0; i < tiers.Count; i++)
        {
            var t = tiers[i];
            if (t == null || !t.HasAnyValidEncounter())
                continue;

            if (t.tier > difficultyTier && t.tier < bestHigherTier)
            {
                bestHigherTier = t.tier;
                higherPool = t;
            }
        }

        if (higherPool != null)
        {
            var higherMatch = higherPool.PickWeightedAny();
            if (higherMatch != null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[EncounterPool:{name}] requestedTier={difficultyTier} -> fallback higher tier={higherPool.tier} picked={higherMatch.name}");
                return higherMatch;
            }
        }

        if (enableDebugLogs)
            Debug.LogWarning($"[EncounterPool:{name}] Pick({difficultyTier}) failed: no valid encounters found.");

        return null;
    }

    [Serializable]
    public sealed class TierPool
    {
        public int tier = 1;
        public List<WeightedEncounter> encounters = new();

        public bool HasAnyValidEncounter()
        {
            if (encounters == null || encounters.Count == 0)
                return false;

            for (int i = 0; i < encounters.Count; i++)
            {
                if (encounters[i].encounter != null)
                    return true;
            }

            return false;
        }

        public bool ContainsEncounterMatchingTier(int requestedTier)
        {
            if (encounters == null || encounters.Count == 0)
                return false;

            for (int i = 0; i < encounters.Count; i++)
            {
                var enc = encounters[i].encounter;
                if (enc != null && enc.MatchesTier(requestedTier))
                    return true;
            }

            return false;
        }

        public EncounterSO PickWeightedForRequestedTier(int requestedTier)
        {
            if (encounters == null || encounters.Count == 0)
                return null;

            List<WeightedEncounter> filtered = new List<WeightedEncounter>();

            for (int i = 0; i < encounters.Count; i++)
            {
                var e = encounters[i];
                if (e.encounter == null)
                    continue;

                if (e.encounter.MatchesTier(requestedTier))
                    filtered.Add(e);
            }

            if (filtered.Count == 0)
                return null;

            return PickWeightedInternal(filtered);
        }

        public EncounterSO PickWeightedAny()
        {
            if (encounters == null || encounters.Count == 0)
                return null;

            List<WeightedEncounter> filtered = new List<WeightedEncounter>();

            for (int i = 0; i < encounters.Count; i++)
            {
                var e = encounters[i];
                if (e.encounter != null)
                    filtered.Add(e);
            }

            if (filtered.Count == 0)
                return null;

            return PickWeightedInternal(filtered);
        }

        private EncounterSO PickWeightedInternal(List<WeightedEncounter> source)
        {
            if (source == null || source.Count == 0)
                return null;

            int total = 0;

            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry.encounter == null)
                    continue;

                int finalWeight = Mathf.Max(0, entry.weight + entry.encounter.relativeWeightBias);
                total += finalWeight;
            }

            if (total <= 0)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i].encounter != null)
                        return source[i].encounter;
                }

                return null;
            }

            int roll = UnityEngine.Random.Range(0, total);

            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry.encounter == null)
                    continue;

                int finalWeight = Mathf.Max(0, entry.weight + entry.encounter.relativeWeightBias);
                if (finalWeight <= 0)
                    continue;

                if (roll < finalWeight)
                    return entry.encounter;

                roll -= finalWeight;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].encounter != null)
                    return source[i].encounter;
            }

            return null;
        }
    }

    [Serializable]
    public struct WeightedEncounter
    {
        public EncounterSO encounter;
        public int weight;
    }
}