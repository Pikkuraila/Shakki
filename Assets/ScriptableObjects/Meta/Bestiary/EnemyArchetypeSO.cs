using UnityEngine;

namespace Shakki.Meta.Bestiary
{
    [CreateAssetMenu(menuName = "Shakki/EnemyArchetype", fileName = "EnemyArchetype")]
    public sealed class EnemyArchetypeSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;                 // "pawn", "rook", ...
        public string displayName;        // can be hidden until discovered
        public Sprite icon;

        [Header("Traits")]
        public EnemyTrait[] traits;

        [Header("Rules")]
        public string rulesetId;          // e.g. "PawnRules", "RookRules" - however you resolve move rules
    }
}
