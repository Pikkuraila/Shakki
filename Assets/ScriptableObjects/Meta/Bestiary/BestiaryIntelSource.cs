namespace Shakki.Meta.Bestiary
{
    public sealed class BestiaryIntelSource : IIntelSource
    {
        private readonly BestiaryService _bestiary;

        public BestiaryIntelSource(BestiaryService bestiary)
        {
            _bestiary = bestiary;
        }

        public void Apply(ref IntelProfile profile, EnemyArchetypeSO archetype, in IntelContext ctx)
        {
            if (archetype == null || _bestiary == null) return;

            // Name
            if (_bestiary.HasUnlock(archetype.id, BestiaryUnlock.NameKnown))
                profile.showName = true;

            // Move
            if (_bestiary.HasUnlock(archetype.id, BestiaryUnlock.MoveKnown))
                profile.UpgradeMoveReveal(MoveRevealMode.HoverPseudo);

            // Optional: traits only after name (or after move) – choose your UX
            if (profile.showName)
                profile.showTraits = true;
        }
    }
}
