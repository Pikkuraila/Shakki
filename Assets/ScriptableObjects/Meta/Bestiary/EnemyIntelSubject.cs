namespace Shakki.Meta.Bestiary
{
    public readonly struct EnemyIntelSubject
    {
        public readonly string id;
        public readonly string displayName;
        public readonly EnemyTrait[] traits;

        public EnemyIntelSubject(string id, string displayName = null, EnemyTrait[] traits = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.traits = traits;
        }
    }
}
