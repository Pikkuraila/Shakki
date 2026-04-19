using System.Collections.Generic;

namespace Shakki.Meta.Bestiary
{
    public sealed class EnemyIntelService
    {
        private readonly HashSet<IntelBuff> _buffs;
        private readonly IntelResolver _resolver;

        public EnemyIntelService()
            : this(new PlayerPrefsBestiaryStore(), new HashSet<IntelBuff>())
        {
        }

        public EnemyIntelService(IBestiaryStore store, HashSet<IntelBuff> buffs)
        {
            _buffs = buffs ?? new HashSet<IntelBuff>();
            _resolver = new IntelResolver(new IIntelSource[]
            {
                new StoredBestiaryIntelSource(store ?? new PlayerPrefsBestiaryStore()),
                new ItemIntelSource(_buffs),
            });
        }

        public IntelProfile Resolve(string enemyId, in IntelContext ctx)
            => Resolve(new EnemyIntelSubject(enemyId), ctx);

        public IntelProfile ResolveInCombat(string enemyId, bool isPlayerTurn)
        {
            var ctx = new IntelContext
            {
                isPlayerTurn = isPlayerTurn,
                inCombat = true
            };

            return Resolve(enemyId, ctx);
        }

        public IntelProfile Resolve(in EnemyIntelSubject subject, in IntelContext ctx)
            => _resolver.Resolve(subject, ctx);

        public void SetBuff(IntelBuff buff, bool enabled)
        {
            if (enabled)
                _buffs.Add(buff);
            else
                _buffs.Remove(buff);
        }
    }
}
