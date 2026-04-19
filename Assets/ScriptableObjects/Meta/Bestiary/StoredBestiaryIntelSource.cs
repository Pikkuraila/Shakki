namespace Shakki.Meta.Bestiary
{
    /// <summary>
    /// Reads persisted bestiary unlocks directly from the configured store so
    /// intel can be resolved in runtime UI code without a direct RunController reference.
    /// </summary>
    public sealed class StoredBestiaryIntelSource : IIntelSource
    {
        private readonly IBestiaryStore _store;

        public StoredBestiaryIntelSource(IBestiaryStore store)
        {
            _store = store;
        }

        public void Apply(ref IntelProfile profile, in EnemyIntelSubject subject, in IntelContext ctx)
        {
            if (_store == null || string.IsNullOrEmpty(subject.id))
                return;

            var data = _store.Load();
            var normId = BestiaryIds.Normalize(subject.id);

            if (data?.entries == null)
                return;

            for (int i = 0; i < data.entries.Count; i++)
            {
                var entry = data.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.archetypeId))
                    continue;

                if (BestiaryIds.Normalize(entry.archetypeId) != normId)
                    continue;

                if ((entry.unlocks & BestiaryUnlock.NameKnown) != 0)
                    profile.showName = true;

                if ((entry.unlocks & BestiaryUnlock.MoveKnown) != 0)
                    profile.UpgradeMoveReveal(MoveRevealMode.HoverPseudo);

                if (profile.showName)
                    profile.showTraits = true;

                return;
            }
        }
    }
}
