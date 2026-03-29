using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Encounter", fileName = "Encounter")]
public sealed class EncounterSO : ScriptableObject
{
    [Header("Win condition")]
    public bool requireWhiteKing = true;
    public bool requireBlackKing = true;

    [Header("Difficulty")]
    [Min(1)] public int minRecommendedTier = 1;
    [Min(1)] public int maxRecommendedTier = 999;

    [Tooltip("Lis‰paino kun t‰m‰ encounter kilpailee muiden saman tierin encounterien kanssa. Voi olla negatiivinen.")]
    [Range(-10, 10)] public int relativeWeightBias = 0;

    [Tooltip("Jos true, t‰t‰ encounteria saa k‰ytt‰‰ boss-poolissa / bossimaisena presetin‰.")]
    public bool allowAsBoss = false;

    [Serializable]
    public struct Spawn
    {
        public string owner;   // "white" tai "black"
        public string pieceId; // esim "King", "Queen", "Rook"
        public int x;          // ABSOLUUTTINEN X
        public int y;          // ABSOLUUTTINEN Y
    }

    [Tooltip("Nappuloiden suorat absoluuttiset spawnauskohdat.")]
    public List<Spawn> spawns = new();

    public bool MatchesTier(int tier)
    {
        int min = Mathf.Max(1, minRecommendedTier);
        int max = Mathf.Max(min, maxRecommendedTier);
        return tier >= min && tier <= max;
    }

    private void OnValidate()
    {
        minRecommendedTier = Mathf.Max(1, minRecommendedTier);
        maxRecommendedTier = Mathf.Max(minRecommendedTier, maxRecommendedTier);
    }
}