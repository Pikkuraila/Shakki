using System.Collections.Generic;
using UnityEngine;

public static class EnemyBudgetGenerator
{
    public static List<string> Generate(
        EnemyTierPoolSO pool,
        int difficulty,
        System.Random rng)
    {
        var result = new List<string>();
        if (pool == null || pool.pieces.Count == 0)
            return result;

        int budget = rng.Next(pool.minBudget, pool.maxBudget + 1);
        budget += Mathf.Max(0, difficulty - pool.tier);

        int safety = 100;
        while (budget > 0 && safety-- > 0)
        {
            var wp = PickWeighted(pool.pieces, rng);
            if (wp.piece == null) continue;

            if (wp.piece.cost <= budget)
            {
                result.Add(wp.piece.typeName);
                budget -= wp.piece.cost;
            }
        }

        // fallbackfill: vähintään yksi Pawn
        if (result.Count == 0)
            result.Add("Pawn");

        return result;
    }

    static EnemyTierPoolSO.WeightedPiece PickWeighted(
        List<EnemyTierPoolSO.WeightedPiece> list,
        System.Random rng)
    {
        int total = 0;
        foreach (var w in list)
            total += Mathf.Max(0, w.weight);

        int roll = rng.Next(0, total);
        foreach (var w in list)
        {
            int weight = Mathf.Max(0, w.weight);
            if (roll < weight)
                return w;
            roll -= weight;
        }

        return list[0];
    }
}
