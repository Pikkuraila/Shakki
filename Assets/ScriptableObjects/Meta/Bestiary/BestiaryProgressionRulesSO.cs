using UnityEngine;

namespace Shakki.Meta.Bestiary
{
    [CreateAssetMenu(menuName = "Shakki/BestiaryProgressionRules", fileName = "BestiaryProgressionRules")]
    public sealed class BestiaryProgressionRulesSO : ScriptableObject
    {
        [Header("Thresholds")]
        public int seenForName = 10;
        public int killsForMove = 10;

        [Header("Both ways unlock (OR)")]
        public bool allowKillsToUnlockName = true;   // "tappamalla oppii nimen"
        public bool allowSeenToUnlockMove = true;    // "näkemällä oppii liikkeen"

        public void ApplyProgress(BestiaryEntrySave e)
        {
            if (e == null) return;

            // NameKnown
            bool nameBySeen = e.seen >= seenForName;
            bool nameByKills = allowKillsToUnlockName && e.kills >= seenForName;
            if (nameBySeen || nameByKills) e.unlocks |= BestiaryUnlock.NameKnown;

            // MoveKnown
            bool moveByKills = e.kills >= killsForMove;
            bool moveBySeen = allowSeenToUnlockMove && e.seen >= killsForMove;
            if (moveByKills || moveBySeen) e.unlocks |= BestiaryUnlock.MoveKnown;
        }
    }
}
