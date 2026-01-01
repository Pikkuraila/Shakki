using System.Collections.Generic;

namespace Shakki.Meta.Bestiary
{
    public enum IntelBuff
    {
        AlwaysShowPseudoMovesAllEnemies,
        HoverShowLegalMovesAllEnemies,
        AlwaysShowLegalMovesAllEnemies,
        // later: ShowAttacks, ShowSpecials, ShowWeakness, etc.
    }

    public sealed class ItemIntelSource : IIntelSource
    {
        private readonly HashSet<IntelBuff> _buffs;

        public ItemIntelSource(HashSet<IntelBuff> activeBuffs)
        {
            _buffs = activeBuffs ?? new HashSet<IntelBuff>();
        }

        public void Apply(ref IntelProfile profile, EnemyArchetypeSO archetype, in IntelContext ctx)
        {
            if (_buffs.Contains(IntelBuff.AlwaysShowLegalMovesAllEnemies))
            {
                profile.UpgradeMoveReveal(MoveRevealMode.AlwaysLegal);
                profile.showName = true;
                profile.showTraits = true;
                return;
            }

            if (_buffs.Contains(IntelBuff.HoverShowLegalMovesAllEnemies))
            {
                profile.UpgradeMoveReveal(MoveRevealMode.HoverLegal);
                profile.showName = true;
                profile.showTraits = true;
            }

            if (_buffs.Contains(IntelBuff.AlwaysShowPseudoMovesAllEnemies))
            {
                profile.UpgradeMoveReveal(MoveRevealMode.AlwaysPseudo);
            }
        }
    }
}
