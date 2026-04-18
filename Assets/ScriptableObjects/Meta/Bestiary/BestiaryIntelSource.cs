namespace Shakki.Meta.Bestiary
{
    public sealed class BestiaryIntelSource : IIntelSource
    {
        private readonly BestiaryService _bestiary;

        public BestiaryIntelSource(BestiaryService bestiary)
        {
            _bestiary = bestiary;
        }

        public void Apply(ref IntelProfile profile, in EnemyIntelSubject subject, in IntelContext ctx)
        {
            if (string.IsNullOrEmpty(subject.id) || _bestiary == null)
                return;

            if (_bestiary.HasUnlock(subject.id, BestiaryUnlock.NameKnown))
                profile.showName = true;

            if (_bestiary.HasUnlock(subject.id, BestiaryUnlock.MoveKnown))
                profile.UpgradeMoveReveal(MoveRevealMode.HoverPseudo);

            if (profile.showName)
                profile.showTraits = true;
        }
    }
}
