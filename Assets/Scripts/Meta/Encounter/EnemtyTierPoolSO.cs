using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/Enemy Tier Pool", fileName = "EnemyTierPool")]
public sealed class EnemyTierPoolSO : ScriptableObject
{
    public int tier = 1;

    [Header("Budget")]
    public int minBudget = 3;
    public int maxBudget = 6;

    [Header("Piece Pool")]
    public List<WeightedPiece> pieces = new();

    [Serializable]
    public struct WeightedPiece
    {
        public PieceDefSO piece;
        public int weight;
    }
}
