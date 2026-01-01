namespace Shakki.Meta.Bestiary
{
    public interface IIntelSource
    {
        void Apply(ref IntelProfile profile, EnemyArchetypeSO archetype, in IntelContext ctx);
    }
}
