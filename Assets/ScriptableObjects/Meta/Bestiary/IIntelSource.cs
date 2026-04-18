namespace Shakki.Meta.Bestiary
{
    public interface IIntelSource
    {
        void Apply(ref IntelProfile profile, in EnemyIntelSubject subject, in IntelContext ctx);
    }
}
