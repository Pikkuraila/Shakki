using System.Collections.Generic;
using UnityEngine;

namespace Shakki.Meta.Bestiary
{
    public sealed class BestiaryDebugDriver : MonoBehaviour
    {
        [SerializeField] private BestiaryProgressionRulesSO rules;

        private BestiaryService _bestiary;
        private IntelResolver _intel;

        void Awake()
        {
            _bestiary = new BestiaryService(rules, new PlayerPrefsBestiaryStore());

            // Example: pretend we have an item buff active (comment out to test pure bestiary)
            var buffs = new HashSet<IntelBuff>();
            // buffs.Add(IntelBuff.AlwaysShowPseudoMovesAllEnemies);

            _intel = new IntelResolver(new IIntelSource[]
            {
                new BestiaryIntelSource(_bestiary),
                new ItemIntelSource(buffs)
            });
        }

        [ContextMenu("TEST: Seen pawn x10")]
        public void TestSeenPawn10()
        {
            _bestiary.RecordSeen("pawn", 10);
            Dump("pawn");
        }

        [ContextMenu("TEST: Kill pawn x10")]
        public void TestKillPawn10()
        {
            _bestiary.RecordKill("pawn", 10);
            Dump("pawn");
        }

        private void Dump(string id)
        {
            var e = _bestiary.GetEntry(id);
            Debug.Log($"[Bestiary] {id}: seen={e?.seen} kills={e?.kills} unlocks={e?.unlocks}");

            // Resolve intel without needing a real SO instance
            var fake = ScriptableObject.CreateInstance<EnemyArchetypeSO>();
            fake.id = id;

            var p = _intel.Resolve(fake, IntelContext.Default);
            Debug.Log($"[Intel] {id}: showName={p.showName} showTraits={p.showTraits} moveReveal={p.moveReveal}");

            Destroy(fake);
        }
    }
}
