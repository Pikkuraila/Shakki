using System;

namespace Shakki.Core
{
    public enum RollVisualType
    {
        Coin,
        Die
    }

    [Serializable]
    public class RandomRollRequest
    {
        public int sides = 2;               // 2 = coin, 6 = d6, 20 = d20
        public int targetValue = 2;         // success threshold
        public int modifier = 0;            // item buffs etc
        public bool higherOrEqualWins = true;

        public RollVisualType visualType = RollVisualType.Coin;

        public string label;

        public Action<int> onResolved;
        public Action onSuccess;
        public Action onFail;
    }
}